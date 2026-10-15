// Copyright 2026, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Moq;
using Moq.Protected;
using Pulumi.Testing;
using Pulumirpc;
using Xunit;

namespace Pulumi.Tests
{
    public class StateMigrationTests
    {
        private const string Urn = "urn:pulumi:stack::project::test:index:Component::component";
        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);

        private static async Task<StateMigrationResponse> Invoke(
            Callbacks callbacks, string token, string state = "[]", CancellationToken cancellationToken = default)
        {
            var context = new Mock<ServerCallContext>();
            context.Protected().SetupGet<CancellationToken>("CancellationTokenCore").Returns(cancellationToken);
            var response = await callbacks.Invoke(new CallbackInvokeRequest
            {
                Token = token,
                Request = new StateMigrationRequest { Urn = Urn, OldState = ByteString.CopyFromUtf8(state) }.ToByteString(),
            }, context.Object).WaitAsync(TestTimeout);
            return StateMigrationResponse.Parser.ParseFrom(response.Response);
        }

        [Fact]
        public async Task CallbackPreservesCheckpointJsonAndSuccessors()
        {
            const string state = """
                [{
                    "urn": "old",
                    "inputs": {
                        "secret": {
                            "4dabf18193072939515e22adb298388d": "1b47061264138c4ac30d75fd1eb44270",
                            "plaintext": "\"sensitive\""
                        },
                        "large": 9007199254740993,
                        "precise": 0.12345678901234567890123456789
                    },
                    "future": {"enabled": true, "items": [null, "x"]}
                }]
                """;
            var callbacks = new Callbacks(Task.FromResult("unused"), null);
            var callback = await Deployment.AllocateStateMigration(callbacks, (args, _) =>
            {
                Assert.Equal(Urn, args.Urn);
                args.OldState[0]!["urn"] = "new";
                return Task.FromResult<StateMigrationResult?>(new StateMigrationResult(args.OldState,
                    new Dictionary<string, string> { ["old"] = "new" }));
            });

            var response = await Invoke(callbacks, callback.Token, state);
            var expected = Assert.IsType<JsonArray>(JsonNode.Parse(state));
            var expectedResource = Assert.IsType<JsonObject>(Assert.Single(expected));
            expectedResource["urn"] = "new";
            Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(response.NewState.ToStringUtf8())));
            Assert.Single(response.Successors);
            Assert.Equal("new", response.Successors["old"]);
        }

        [Fact]
        public async Task NullResultDiscardsMutationsAndEmptyArrayIsExplicit()
        {
            var callbacks = new Callbacks(Task.FromResult("unused"), null);
            var noop = await Deployment.AllocateStateMigration(callbacks, (args, _) =>
            {
                args.OldState.Add(new JsonObject());
                return Task.FromResult<StateMigrationResult?>(null);
            });
            var response = await Invoke(callbacks, noop.Token);
            Assert.True(response.NewState.IsEmpty);
            Assert.Empty(response.Successors);

            var empty = await Deployment.AllocateStateMigration(callbacks, (_, _) =>
                Task.FromResult<StateMigrationResult?>(new StateMigrationResult(new JsonArray())));
            var emptyState = (await Invoke(callbacks, empty.Token)).NewState.ToStringUtf8();
            Assert.Empty(Assert.IsType<JsonArray>(JsonNode.Parse(emptyState)));
        }

        [Fact]
        public async Task CallbackFailureFailsTheRpc()
        {
            var callbacks = new Callbacks(Task.FromResult("unused"), null);
            var callback = await Deployment.AllocateStateMigration(callbacks, async (_, _) =>
            {
                await Task.Yield();
                throw new InvalidOperationException("migration failed");
            });
            var error = await Assert.ThrowsAsync<RpcException>(() => Invoke(callbacks, callback.Token));
            Assert.Contains("migration failed", error.Status.Detail);
        }

        [Theory]
        [InlineData("not json")]
        [InlineData("{}")]
        [InlineData("null")]
        public async Task InvalidStateFailsBeforeInvokingCallback(string state)
        {
            var callbacks = new Callbacks(Task.FromResult("unused"), null);
            var called = false;
            var callback = await Deployment.AllocateStateMigration(callbacks, (_, _) =>
            {
                called = true;
                return Task.FromResult<StateMigrationResult?>(null);
            });

            await Assert.ThrowsAsync<RpcException>(() => Invoke(callbacks, callback.Token, state));
            Assert.False(called);
        }

        [Fact]
        public async Task CallbackReceivesRpcCancellationToken()
        {
            using var cancellation = new CancellationTokenSource();
            var receivedToken = default(CancellationToken);
            var callbacks = new Callbacks(Task.FromResult("unused"), null);
            var callback = await Deployment.AllocateStateMigration(callbacks, (_, token) =>
            {
                receivedToken = token;
                return Task.FromResult<StateMigrationResult?>(null);
            });

            await Invoke(callbacks, callback.Token, cancellationToken: cancellation.Token);
            Assert.Equal(cancellation.Token, receivedToken);
        }

        [Fact]
        public async Task GuardFlowsAcrossAwaitAndIsIsolatedFromOtherCallbacks()
        {
            var callbacks = new Callbacks(Task.FromResult("unused"), ExecutionContext.Capture());
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var migration = await Deployment.AllocateStateMigration(callbacks, async (_, _) =>
            {
                entered.SetResult(true);
                await release.Task;
                var error = Assert.Throws<InvalidOperationException>(() =>
                    new ComponentResource("test:index:Component", "forbidden"));
                Assert.Contains(Urn, error.Message);
                return null;
            });
            var pending = Invoke(callbacks, migration.Token);
            try
            {
                await entered.Task.WaitAsync(TestTimeout);
                StateMigrationContext.EnsureNotActive("outside callback");
                var ordinary = await callbacks.AllocateCallback((_, _) =>
                {
                    StateMigrationContext.EnsureNotActive("ordinary callback");
                    return Task.FromResult<IMessage>(new StateMigrationResponse());
                });
                await Invoke(callbacks, ordinary.Token);
            }
            finally
            {
                release.SetResult(true);
            }
            await pending;
        }

        [Fact]
        public void OptionsCopyAndMergePreserveOrderWithoutSharingLists()
        {
            StateMigration first = (_, _) => Task.FromResult<StateMigrationResult?>(null);
            StateMigration second = (_, _) => Task.FromResult<StateMigrationResult?>(null);
            var original = new ComponentResourceOptions { StateMigrations = { first } };
            var merged = ComponentResourceOptions.Merge(original,
                new ComponentResourceOptions { StateMigrations = { second } });
            Assert.Equal(new[] { first, second }, merged.StateMigrations);
            merged.StateMigrations.Clear();
            Assert.Single(original.StateMigrations);
            var clone = original.Clone();
            clone.StateMigrations.Clear();
            Assert.Single(original.StateMigrations);
            var custom = CustomResourceOptions.Merge(new CustomResourceOptions { StateMigrations = { first } },
                new CustomResourceOptions { StateMigrations = { second } });
            Assert.Equal(new[] { first, second }, custom.StateMigrations);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task RegistrationChecksFeatureAndSendsOrderedCallbacks(bool supported, bool hasMigrations)
        {
            var requests = new List<RegisterResourceRequest>();
            var monitor = new Mock<IMonitor>();
            monitor.Setup(m => m.SupportsFeatureAsync(It.IsAny<SupportsFeatureRequest>()))
                .ReturnsAsync((SupportsFeatureRequest request) => new SupportsFeatureResponse
                {
                    HasSupport = request.Id != "stateMigrations" || supported,
                });
            monitor.Setup(m => m.RegisterResourceAsync(It.IsAny<Resource>(), It.IsAny<RegisterResourceRequest>()))
                .Returns(async (Resource _, RegisterResourceRequest request) =>
                {
                    requests.Add(request);
                    // Invoke the actual callback server while resource registration is still pending.
                    foreach (var callback in request.StateMigrations)
                    {
                        using var channel = GrpcChannel.ForAddress("http://" + callback.Target);
                        var client = new Pulumirpc.Callbacks.CallbacksClient(channel);
                        await client.InvokeAsync(new CallbackInvokeRequest
                        {
                            Token = callback.Token,
                            Request = new StateMigrationRequest { Urn = Urn, OldState = ByteString.CopyFromUtf8("[]") }.ToByteString(),
                        }, deadline: DateTime.UtcNow.Add(TestTimeout));
                    }
                    return new RegisterResourceResponse { Urn = Urn + request.Name, Object = request.Object };
                });
            monitor.Setup(m => m.RegisterResourceOutputsAsync(It.IsAny<RegisterResourceOutputsRequest>()))
                .Returns(Task.CompletedTask);
            monitor.Setup(m => m.SignalAndWaitForShutdownAsync()).Returns(Task.CompletedTask);

            var calls = new List<int>();
            StateMigration Migration(int index) => async (_, _) =>
            {
                await Task.Yield();
                Assert.Equal("stack", Deployment.Instance.StackName);
                calls.Add(index);
                return null;
            };

            Task<ComponentResource> Run() =>
                InlineDeploymentTests.TryInline(monitor.Object, async () =>
                {
                    _ = new Stack();
                    var options = new ComponentResourceOptions();
                    if (hasMigrations)
                    {
                        options.StateMigrations.AddRange(new[] { Migration(1), Migration(2) });
                    }
                    var parent = new ComponentResource("test:index:Component", "parent", options);
                    await parent.Urn.GetValueAsync("");
                    var child = new ComponentResource("test:index:Child", "child", new ComponentResourceOptions { Parent = parent });
                    await child.Urn.GetValueAsync("");
                    return parent;
                });

            if (supported || !hasMigrations)
            {
                await Run();
                Assert.Equal(hasMigrations ? new[] { 1, 2 } : Array.Empty<int>(), calls);
                Assert.Empty(requests.Single(r => r.Name == "child").StateMigrations);
            }
            else
            {
                var error = await Assert.ThrowsAnyAsync<Exception>(Run);
                Assert.Contains("The Pulumi CLI does not support state migrations", error.ToString());
                Assert.DoesNotContain(requests, r => r.Name == "parent");
            }
        }
    }
}

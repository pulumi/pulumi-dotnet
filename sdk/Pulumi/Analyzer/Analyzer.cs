using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pulumirpc;

namespace Pulumi.Analyzer
{
    public abstract class PolicyManager
    {
        public abstract void ReportViolation(string description);

        public abstract void ReportViolationWithContext(string description, params PolicyResource[] resourcesInvolved);

        public abstract PolicyResource? FetchResource(string urn);
    }

    internal delegate void PolicyManagerReportViolation(string description);

    internal delegate void PolicyManagerReportViolationWithContext(string description, params PolicyResource[] resourcesInvolved);

    internal delegate PolicyResource? PolicyManagerFetchResource(string urn);

    internal class PolicyManagerDelegator : PolicyManager
    {
        private readonly PolicyManagerReportViolation? _reportViolation;
        private readonly PolicyManagerReportViolationWithContext? _reportViolationWithContext;
        private readonly PolicyManagerFetchResource? _fetchResource;

        public PolicyManagerDelegator(PolicyManagerReportViolation? reportViolation,
            PolicyManagerReportViolationWithContext? reportViolationWithContext,
            PolicyManagerFetchResource? fetchResource)
        {
            this._reportViolation = reportViolation;
            this._reportViolationWithContext = reportViolationWithContext;
            this._fetchResource = fetchResource;
        }

        public override void ReportViolation(string description)
        {
            if (_reportViolation != null)
            {
                _reportViolation(description);
            }
        }

        public override void ReportViolationWithContext(string description, params PolicyResource[] resourcesInvolved)
        {
            if (_reportViolationWithContext != null)
            {
                _reportViolationWithContext(description, resourcesInvolved);
            }
        }

        public override PolicyResource? FetchResource(string urn)
        {
            if (_fetchResource != null)
            {
                return _fetchResource(urn);
            }

            return null;
        }
    }

    public abstract class Bootstrap
    {
        public static async Task<int> RunAsync()
        {
            // ReSharper disable UnusedVariable
            var engine = Environment.GetEnvironmentVariable("PULUMI_ENGINE");
            if (string.IsNullOrEmpty(engine))
            {
                throw new InvalidOperationException("Program run without the Pulumi engine available; re-run using the `pulumi` CLI");
            }

            using var host = BuildHost(engine);

            // before starting the host, set up this callback to tell us what port was selected
            await host.StartAsync(CancellationToken.None);
            var uri = GetHostUri(host);

            var port = uri.Port;
            // Explicitly write just the number and "\n". WriteLine would write "\r\n" on Windows, and while
            // the engine has now been fixed to handle that (see https://github.com/pulumi/pulumi/pull/11915)
            // we work around this here so that old engines can use dotnet providers as well.
            Console.Out.Write(port + "\n");

            await host.WaitForShutdownAsync(CancellationToken.None);
            return 0;
        }

        private static Uri GetHostUri(Microsoft.Extensions.Hosting.IHost host)
        {
            var serverFeatures = host.Services.GetRequiredService<IServer>().Features;
            var addressesFeature = serverFeatures.Get<IServerAddressesFeature>();
            Debug.Assert(addressesFeature != null, "Server should have an IServerAddressesFeature");
            var addresses = addressesFeature.Addresses.ToList();
            Debug.Assert(addresses.Count == 1, "Server should only be listening on one address");
            var uri = new Uri(addresses[0]);
            return uri;
        }

        private static Microsoft.Extensions.Hosting.IHost BuildHost(string engineAddress)
        {
            // maxRpcMessageSize raises the gRPC Max message size from `4194304` (4mb) to `419430400` (400mb)
            var maxRpcMessageSize = 400 * 1024 * 1024;

            return Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .ConfigureKestrel(kestrelOptions =>
                        {
                            kestrelOptions.Listen(IPAddress.Loopback, 0,
                                listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; });
                        })
                        .ConfigureAppConfiguration((context, config) =>
                        {
                            // clear so we don't read appsettings.json
                            // note that we also won't read environment variables for config
                            config.Sources.Clear();

                            var memConfig = new Dictionary<string, string?> { { "Host", engineAddress } };

                            config.AddInMemoryCollection(memConfig);
                        })
                        .ConfigureLogging(loggingBuilder =>
                        {
                            // disable default logging
                            loggingBuilder.ClearProviders();
                        })
                        .ConfigureServices(services =>
                        {
                            services.AddSingleton<AnalyzerService>();

                            services.AddGrpc(grpcOptions =>
                            {
                                grpcOptions.MaxReceiveMessageSize = maxRpcMessageSize;
                                grpcOptions.MaxSendMessageSize = maxRpcMessageSize;
                            });
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseEndpoints(endpoints => { endpoints.MapGrpcService<AnalyzerService>(); });
                        });
                })
                .Build();
        }
    }

    class AnalyzerService : Pulumirpc.Analyzer.AnalyzerBase, IDisposable
    {
        private readonly CancellationTokenSource rootCTS;
        private readonly string version;

        public AnalyzerService()
        {
            this.rootCTS = new CancellationTokenSource();

            var entryAssembly = System.Reflection.Assembly.GetEntryAssembly();
            Debug.Assert(entryAssembly != null, "GetEntryAssembly returned null in managed code");
            var entryName = entryAssembly.GetName();
            var assemblyVersion = entryName.Version;
            if (assemblyVersion == null)
            {
                throw new ArgumentException("Provider.Serve must be called with a version, or an assembly version must be set.");
            }

            // Pulumi expects semver style versions, so we convert from the .NET version format by
            // dropping the revision component.
            this.version = $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
        }

        public void Dispose()
        {
            this.rootCTS.Dispose();
        }

        private static async Task<T> WrapProviderCall<T>(Func<Task<T>> call, [CallerMemberName] string? methodName = default)
        {
            try
            {
                return await call();
            }
            catch (NotImplementedException ex)
            {
                throw new RpcException(new Status(StatusCode.Unimplemented, ex.Message));
            }
            catch (TaskCanceledException ex)
            {
                throw new RpcException(new Status(StatusCode.Cancelled, ex.Message));
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        private CancellationTokenSource GetToken(ServerCallContext context)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(rootCTS.Token, context.CancellationToken);
        }

        // Helper to deal with the fact that at the GRPC layer any Struct property might be null. For those we just want to return empty dictionaries at this level.
        // This keeps the PropertyValue. Unmarshal clean in terms of not handling nulls.
        private static ImmutableDictionary<string, PropertyValue> Unmarshal(Struct? properties)
        {
            if (properties == null)
            {
                return ImmutableDictionary<string, PropertyValue>.Empty;
            }

            return PropertyValue.Unmarshal(properties);
        }

        // Helper to marshal CheckFailures from the domain to the GRPC layer.
        private static IEnumerable<Pulumirpc.CheckFailure> MapFailures(IEnumerable<CheckFailure>? failures)
        {
            if (failures != null)
            {
                foreach (var domFailure in failures)
                {
                    var grpcFailure = new CheckFailure
                    {
                        Property = domFailure.Property,
                        Reason = domFailure.Reason
                    };
                    yield return grpcFailure;
                }
            }
        }

        public override Task<PluginInfo> GetPluginInfo(Empty request, ServerCallContext context)
        {
            return WrapProviderCall(() =>
            {
                // Return basic plugin information
                var info = new PluginInfo
                {
                    Version = version
                };

                return Task.FromResult(info);
            });
        }

        public override Task<AnalyzeResponse> Analyze(AnalyzeRequest request, ServerCallContext context)
        {
            return WrapProviderCall(() =>
            {
                var response = new AnalyzeResponse();

                var resourceType = request.Type;

                foreach (var pack in PolicyPackages.Get())
                {
                    if (pack.ResourcePolicyInputs.TryGetValue(resourceType, out var policy))
                    {
                        var manager = new PolicyManagerDelegator(description =>
                            {
                                var diag = new AnalyzeDiagnostic
                                {
                                    EnforcementLevel = policy.Annotation.EnforcementLevelForRpc,
                                    PolicyPackName = pack.Annotation.Name,
                                    PolicyPackVersion = pack.Annotation.Version,
                                    PolicyName = policy.Annotation.Name,
                                    Description = policy.Annotation.Description,
                                    Urn = request.Urn,
                                    Message = description,
                                };
                                response.Diagnostics.Add(diag);
                            },
                            (description, involved) => { },
                            urn => null
                        );

                        try
                        {
                            var resourceArgs = PolicyResource.Deserialize(request.Properties, policy.ResourceClass, true);

                            Invoke(() => policy.Target.Invoke(null, new[] { manager, resourceArgs }));
                        }
                        catch (UndeferrableValueException e)
                        {
                            var diag = new AnalyzeDiagnostic
                            {
                                EnforcementLevel = policy.Annotation.EnforcementLevelForRpc,
                                PolicyPackName = pack.Annotation.Name,
                                PolicyPackVersion = pack.Annotation.Version,
                                PolicyName = policy.Annotation.Name,
                                Description = policy.Annotation.Description,
                                Urn = request.Urn,
                                Message = $"can't run policy during preview: {e.Message}",
                            };
                            response.Diagnostics.Add(diag);
                        }
                    }
                }

                return Task.FromResult(response);
            });
        }

        public override Task<AnalyzeResponse> AnalyzeStack(AnalyzeStackRequest request, ServerCallContext context)
        {
            return WrapProviderCall(() =>
            {
                var response = new AnalyzeResponse();

                foreach (var pack in PolicyPackages.Get())
                {
                    if (pack.StackPolicy != null)
                    {
                        var manager = new PolicyManagerDelegator(description =>
                            {
                                var diag = new AnalyzeDiagnostic
                                {
                                    PolicyPackName = pack.Annotation.Name,
                                    PolicyPackVersion = pack.Annotation.Version,
                                    PolicyName = pack.StackPolicy.Annotation.Name,
                                    EnforcementLevel = pack.StackPolicy.Annotation.EnforcementLevelForRpc,
                                    Description = pack.StackPolicy.Annotation.Description,
                                    Message = description,
                                };
                                response.Diagnostics.Add(diag);
                            },
                            (description, involved) => { },
                            urn => null
                        );

                        try
                        {
                            var resources = new List<PolicyResourceOutput>();

                            foreach (var res in request.Resources)
                            {
                                var resourceClass = PolicyResourcePackages.ResolveOutputType(res.Type, "");
                                if (resourceClass == null)
                                {
                                    continue;
                                }

                                var resourceArgs = (PolicyResourceOutput)PolicyResource.Deserialize(res.Properties, resourceClass, false);
                                resources.Add(resourceArgs);
                            }

                            Invoke(() => pack.StackPolicy.Target.Invoke(null, new[] { manager, (object)resources }));
                        }
                        catch (UndeferrableValueException e)
                        {
                            var diag = new AnalyzeDiagnostic
                            {
                                EnforcementLevel = Pulumirpc.EnforcementLevel.Advisory,
                                PolicyPackName = pack.Annotation.Name,
                                PolicyPackVersion = pack.Annotation.Version,
                                PolicyName = pack.StackPolicy.Annotation.Name,
                                Description = pack.StackPolicy.Annotation.Description,
                                Message = $"can't run policy during preview: {e.Message}",
                            };
                            response.Diagnostics.Add(diag);
                        }
                    }
                }

                return Task.FromResult(response);
            });
        }

        private static void Invoke(Action runnable)
        {
            try
            {
                runnable();
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException ?? e;
            }
        }

        public override Task<RemediateResponse> Remediate(AnalyzeRequest request, ServerCallContext context)
        {
            return WrapProviderCall(() => { return Task.FromResult(new RemediateResponse()); });
        }

        public override Task<AnalyzerInfo> GetAnalyzerInfo(Empty request, ServerCallContext context)
        {
            return WrapProviderCall(() =>
            {
                foreach (var pack in PolicyPackages.Get())
                {
                    var analyzerInfo = new AnalyzerInfo
                    {
                        Name = pack.Annotation.Name,
                        Version = pack.Annotation.Version,
                    };

                    void AddEntry(PolicyForResource value)
                    {
                        var policyInfo = new PolicyInfo
                        {
                            Name = value.Annotation.Name,
                            Description = value.Annotation.Description,
                            EnforcementLevel = value.Annotation.EnforcementLevelForRpc
                        };

                        analyzerInfo.Policies.Add(policyInfo);
                    }

                    foreach (var entry in pack.ResourcePolicyInputs.Values)
                    {
                        AddEntry(entry);
                    }

                    foreach (var entry in pack.ResourcePolicyOutputs.Values)
                    {
                        AddEntry(entry);
                    }

                    return Task.FromResult(analyzerInfo);
                }

                throw new ArgumentException("No Policy package found");
            });
        }

        public override Task<Empty> Configure(ConfigureAnalyzerRequest request, ServerCallContext context)
        {
            return WrapProviderCall(() => Task.FromResult(new Empty()));
        }
    }
}

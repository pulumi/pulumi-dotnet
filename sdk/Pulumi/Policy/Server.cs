// Copyright 2025, Pulumi Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using System.Globalization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Pulumirpc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Runtime.CompilerServices;

namespace Pulumi.Experimental.Policy
{
    public sealed partial class PolicyPack
    {
        public static async Task Serve(string[] args, string? version, Func<Experimental.IEngine, PolicyPack> factory, CancellationToken cancellationToken)
        {
            var value = System.Environment.GetEnvironmentVariable("PULUMI_ATTACH_DEBUGGER");
            if (value != null && value == "true")
            {
                while (!System.Diagnostics.Debugger.IsAttached)
                {
                    // keep waiting until the debugger is attached
                    System.Threading.Thread.Sleep(1);
                }
            }
            using var host = BuildHost(args, version, GrpcDeploymentBuilder.Instance, factory);

            // before starting the host, set up this callback to tell us what port was selected
            await host.StartAsync(cancellationToken);
            var uri = GetHostUri(host);

            var port = uri.Port;
            // Explicitly write just the number and "\n". WriteLine would write "\r\n" on Windows, and while
            // the engine has now been fixed to handle that (see https://github.com/pulumi/pulumi/pull/11915)
            // we work around this here so that old engines can use dotnet providers as well.
            Console.Write(port.ToString(CultureInfo.InvariantCulture) + "\n");

            await host.WaitForShutdownAsync(cancellationToken);
        }

        static Uri GetHostUri(Microsoft.Extensions.Hosting.IHost host)
        {
            var serverFeatures = host.Services.GetRequiredService<IServer>().Features;
            var addressesFeature = serverFeatures.Get<IServerAddressesFeature>();
            Debug.Assert(addressesFeature != null, "Server should have an IServerAddressesFeature");
            var addresses = addressesFeature.Addresses.ToList();
            Debug.Assert(addresses.Count == 1, "Server should only be listening on one address");
            var uri = new Uri(addresses[0]);
            return uri;
        }


        internal static Microsoft.Extensions.Hosting.IHost BuildHost(
            string[] args,
            string? version,
            IDeploymentBuilder deploymentBuilder,
            Func<Experimental.IEngine, PolicyPack> factory,
            Action<IWebHostBuilder>? configuration = default)
        {
            // maxRpcMessageSize raises the gRPC Max message size from `4194304` (4mb) to `419430400` (400mb)
            var maxRpcMessageSize = 400 * 1024 * 1024;

            var engineAddress = GetEngineAddress(args);

            return Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .ConfigureKestrel(kestrelOptions =>
                        {
                            kestrelOptions.Listen(IPAddress.Loopback, 0, listenOptions =>
                            {
                                listenOptions.Protocols = HttpProtocols.Http2;
                            });
                        })
                        .ConfigureAppConfiguration((context, config) =>
                        {
                            // clear so we don't read appsettings.json
                            // note that we also won't read environment variables for config
                            config.Sources.Clear();

                            var memConfig = new Dictionary<string, string?>();
                            if (engineAddress != null)
                            {
                                memConfig.Add("Host", engineAddress);
                            }
                            if (version != null)
                            {
                                memConfig.Add("Version", version);
                            }
                            config.AddInMemoryCollection(memConfig);
                        })
                        .ConfigureLogging(loggingBuilder =>
                        {
                            // disable default logging
                            loggingBuilder.ClearProviders();
                        })
                        .ConfigureServices(services =>
                        {
                            services.AddSingleton(factory);
                            services.AddSingleton(deploymentBuilder);
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
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapGrpcService<AnalyzerService>();
                            });
                        });
                    configuration?.Invoke(webBuilder);
                })
                .Build();
        }

        private static string? GetEngineAddress(string[] args)
        {
            var cleanArgs = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                // Skip logging-related arguments
                if (arg == "--logtostderr") continue;
                if (arg.StartsWith("-v", StringComparison.Ordinal)) continue;
                if (arg == "--logflow") continue;
                if (arg == "--tracing")
                {
                    i++; // Skip the tracing value
                    continue;
                }

                cleanArgs.Add(arg);
            }

            if (cleanArgs.Count == 0)
            {
                return null;
            }

            if (cleanArgs.Count > 1)
            {
                throw new ArgumentException(
                    $"Expected at most one engine address argument, but got {cleanArgs.Count} non-logging arguments");
            }

            return cleanArgs[0];
        }
    }

    class AnalyzerService : Analyzer.AnalyzerBase, IDisposable
    {
        private readonly Func<Experimental.IEngine, PolicyPack> factory;
        private readonly IDeploymentBuilder deploymentBuilder;
        private readonly ILogger? logger;
        private readonly CancellationTokenSource rootCTS;
        private PolicyPack? implementation;
        private readonly string version;
        private Dictionary<string, PolicyConfig> config = new Dictionary<string, PolicyConfig>();

        PolicyPack Implementation
        {
            get
            {
                if (implementation == null)
                {
                    throw new RpcException(new Status(StatusCode.FailedPrecondition, "Engine host not yet attached"));
                }
                return implementation;
            }
        }

        private IEngine CreateAnalyzer(string address)
        {
            var engine = deploymentBuilder.BuildEngine(address);
            implementation = factory(engine);
            return engine;
        }

        public AnalyzerService(Func<Experimental.IEngine, PolicyPack> factory,
            IDeploymentBuilder deploymentBuilder,
            IConfiguration configuration,
            ILogger<AnalyzerService>? logger)
        {
            this.factory = factory;
            this.deploymentBuilder = deploymentBuilder;
            this.logger = logger;
            this.rootCTS = new CancellationTokenSource();

            var engineAddress = configuration.GetValue<string?>("Host", null);
            if (engineAddress != null)
            {
                CreateAnalyzer(engineAddress);
            }

            var version = configuration.GetValue<string?>("Version", null);
            if (version == null)
            {
                var entryAssembly = System.Reflection.Assembly.GetEntryAssembly();
                Debug.Assert(entryAssembly != null, "GetEntryAssembly returned null in managed code");
                var entryName = entryAssembly.GetName();
                var assemblyVersion = entryName.Version;
                if (assemblyVersion != null)
                {
                    // Pulumi expects semver style versions, so we convert from the .NET version format by
                    // dropping the revision component.
                    version = string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", assemblyVersion.Major, assemblyVersion.Minor, assemblyVersion.Build);
                }
                else
                {
                    throw new InvalidOperationException("Provider.Serve must be called with a version, or an assembly version must be set.");
                }
            }
            this.version = version;
        }

        public void Dispose()
        {
            this.rootCTS.Dispose();
        }

        private async Task<T> WrapAnalyzerCall<T>(Func<Task<T>> call, [CallerMemberName] string? methodName = default)
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
                logger?.LogError(ex, "Error calling {MethodName}.", methodName);
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }


        public override Task<AnalyzerHandshakeResponse> Handshake(AnalyzerHandshakeRequest request, ServerCallContext context)
        {
            return WrapAnalyzerCall(async () =>
            {
                var engine = CreateAnalyzer(request.EngineAddress);
                await Implementation.HandshakeAsync(
                    new HandshakeRequest(engine, request.RootDirectory, request.ProgramDirectory),
                    context.CancellationToken);
                return new AnalyzerHandshakeResponse();
            });
        }

        public override Task<PluginInfo> GetPluginInfo(Empty request, ServerCallContext context)
        {
            return Task.FromResult(new PluginInfo
            {
                Version = version,
            });
        }

        public override Task<AnalyzerInfo> GetAnalyzerInfo(Empty request, ServerCallContext context)
        {
            return WrapAnalyzerCall(() =>
            {
                var policies = new List<PolicyInfo>();
                foreach (var p in Implementation.Policies)
                {
                    policies.Add(new PolicyInfo
                    {
                        Name = p.Name,
                        Description = p.Description,
                        EnforcementLevel = (Pulumirpc.EnforcementLevel)p.EnforcementLevel,
                    });
                }

                return Task.FromResult(new AnalyzerInfo
                {
                    Name = Implementation.Name,
                    Version = version,
                    Policies = { policies },
                    SupportsConfig = true,
                });
            });
        }

        static object? ToObject(Google.Protobuf.WellKnownTypes.Value protoValue)
        {
            switch (protoValue.KindCase)
            {
                case Value.KindOneofCase.NullValue:
                    return null;
                case Value.KindOneofCase.NumberValue:
                    return protoValue.NumberValue;
                case Value.KindOneofCase.StringValue:
                    return protoValue.StringValue;
                case Value.KindOneofCase.BoolValue:
                    return protoValue.BoolValue;
                case Value.KindOneofCase.StructValue:
                    return ToObject(protoValue.StructValue);
                case Value.KindOneofCase.ListValue:
                    return protoValue.ListValue.Values.Select(ToObject).ToList();
            }

            throw new ArgumentException($"Unsupported Value type: {protoValue.KindCase}");
        }

        static Dictionary<string, object?> ToObject(Google.Protobuf.WellKnownTypes.Struct protoStruct)
        {
            var result = new Dictionary<string, object?>();
            foreach (var kvp in protoStruct.Fields)
            {
                result.Add(kvp.Key, ToObject(kvp.Value));
            }
            return result;
        }

        public override Task<Empty> Configure(ConfigureAnalyzerRequest request, ServerCallContext context)
        {
            return WrapAnalyzerCall(() =>
            {
                var conf = new Dictionary<string, PolicyConfig>();
                foreach (var kvp in request.PolicyConfig)
                {
                    var k = kvp.Key;
                    var v = kvp.Value;
                    var props = ToObject(v.Properties);

                    conf[k] = new PolicyConfig(
                        (EnforcementLevel)v.EnforcementLevel,
                        props);
                }
                config = conf;
                return Task.FromResult(new Empty());
            });
        }

        public override Task<AnalyzeResponse> Analyze(AnalyzeRequest request, ServerCallContext context)
        {
            return WrapAnalyzerCall(async () =>
            {
                var diagnostics = new List<AnalyzeDiagnostic>();

                foreach (var p in Implementation.Policies)
                {
                    if (p is ResourceValidationPolicy resourcePolicy)
                    {
                        var policyManager = new PolicyManager((message, urn) =>
                        {
                            if (string.IsNullOrEmpty(urn))
                                urn = request.Urn;

                            var violationMessage = p.Description;
                            if (!string.IsNullOrEmpty(message))
                                violationMessage += "\n" + message;

                            diagnostics.Add(new AnalyzeDiagnostic
                            {
                                PolicyName = p.Name,
                                PolicyPackName = Implementation.Name,
                                PolicyPackVersion = version,
                                Description = p.Description,
                                Message = violationMessage,
                                EnforcementLevel = (Pulumirpc.EnforcementLevel)p.EnforcementLevel,
                                Urn = urn,
                            });
                        });

                        PolicyConfig? c;
                        this.config.TryGetValue(p.Name, out c);

                        var pm = PropertyValue.Unmarshal(request.Properties);

                        var resource = new AnalyzerResource
                        {
                            Type = request.Type,
                            Properties = pm,
                            URN = request.Urn,
                            Name = request.Name,
                            // TODO: Fill in Options, Provider, Parent, Dependencies, PropertyDependencies
                        };

                        var args = new ResourceValidationArgs(policyManager, resource, c == null ? null : c.Properties);

                        await resourcePolicy.ValidateAsync(args, context.CancellationToken);
                    }
                }

                return new AnalyzeResponse
                {
                    Diagnostics = { diagnostics }
                };
            });
        }

        public override Task<AnalyzeResponse> AnalyzeStack(AnalyzeStackRequest request, ServerCallContext context)
        {
            // TODO: Implement stack analysis
            return Task.FromResult(new AnalyzeResponse());
        }
    }
}

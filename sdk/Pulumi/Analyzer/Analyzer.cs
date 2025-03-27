using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Pulumirpc;

namespace Pulumi.Analyzer
{
    public abstract class Analyzer
    {
        public static Task Serve(string[] args, string? version, Func<IHost, Analyzer> factory, CancellationToken cancellationToken)
        {
            return Serve(args, version, factory, cancellationToken, System.Console.Out);
        }

        public static async Task Serve(string[] args,
            string? version,
            Func<IHost, Analyzer> factory,
            CancellationToken cancellationToken,
            System.IO.TextWriter stdout)
        {
            using var host = BuildHost(args, version, GrpcDeploymentBuilder.Instance, factory);

            // before starting the host, set up this callback to tell us what port was selected
            await host.StartAsync(cancellationToken);
            var uri = GetHostUri(host);

            var port = uri.Port;
            // Explicitly write just the number and "\n". WriteLine would write "\r\n" on Windows, and while
            // the engine has now been fixed to handle that (see https://github.com/pulumi/pulumi/pull/11915)
            // we work around this here so that old engines can use dotnet providers as well.
            stdout.Write(port.ToString() + "\n");

            await host.WaitForShutdownAsync(cancellationToken);
        }

        public static Uri GetHostUri(Microsoft.Extensions.Hosting.IHost host)
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
            Func<IHost, Analyzer> factory,
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
                            kestrelOptions.Listen(IPAddress.Loopback, 0,
                                listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; });
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
                            // to be injected into ResourceProviderService
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
                            app.UseEndpoints(endpoints => { endpoints.MapGrpcService<AnalyzerService>(); });
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
                if (arg.StartsWith("-v")) continue;
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

    class AnalyzerService : Pulumirpc.Analyzer.AnalyzerBase, IDisposable
    {
        private readonly Func<IHost, Analyzer> factory;
        private readonly IDeploymentBuilder deploymentBuilder;
        private readonly ILogger? logger;
        private readonly CancellationTokenSource rootCTS;
        private Analyzer? implementation;
        private readonly string version;
        private string? engineAddress;

        Analyzer Implementation
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

        string EngineAddress => engineAddress ??
                                throw new RpcException(new Status(StatusCode.FailedPrecondition,
                                    "Engine host not yet attached"));

        private void CreateProvider(string address)
        {
            var host = new GrpcHost(address);
            implementation = factory(host);
            engineAddress = address;
        }

        public AnalyzerService(Func<IHost, Analyzer> factory,
            IDeploymentBuilder deploymentBuilder,
            IConfiguration configuration,
            ILogger<AnalyzerService>? logger)
        {
            this.factory = factory;
            this.deploymentBuilder = deploymentBuilder;
            this.logger = logger;
            this.rootCTS = new CancellationTokenSource();

            engineAddress = configuration.GetValue<string?>("Host", null);
            if (engineAddress != null)
            {
                CreateProvider(engineAddress);
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
                    version = string.Format("{0}.{1}.{2}", assemblyVersion.Major, assemblyVersion.Minor,
                        assemblyVersion.Build);
                }
                else
                {
                    throw new Exception(
                        "Provider.Serve must be called with a version, or an assembly version must be set.");
                }
            }

            this.version = version;
        }

        public void Dispose()
        {
            this.rootCTS.Dispose();
        }

        private async Task<T> WrapProviderCall<T>(Func<Task<T>> call, [CallerMemberName] string? methodName = default)
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

        private CancellationTokenSource GetToken(ServerCallContext context)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(rootCTS.Token, context.CancellationToken);
        }

        // Helper to deal with the fact that at the GRPC layer any Struct property might be null. For those we just want to return empty dictionaries at this level.
        // This keeps the PropertyValue. Unmarshal clean in terms of not handling nulls.
        private ImmutableDictionary<string, PropertyValue> Unmarshal(Struct? properties)
        {
            if (properties == null)
            {
                return ImmutableDictionary<string, PropertyValue>.Empty;
            }

            return PropertyValue.Unmarshal(properties);
        }

        // Helper to marshal CheckFailures from the domain to the GRPC layer.
        private IEnumerable<Pulumirpc.CheckFailure> MapFailures(IEnumerable<CheckFailure>? failures)
        {
            if (failures != null)
            {
                foreach (var domFailure in failures)
                {
                    var grpcFailure = new Pulumirpc.CheckFailure();
                    grpcFailure.Property = domFailure.Property;
                    grpcFailure.Reason = domFailure.Reason;
                    yield return grpcFailure;
                }
            }
        }

        public override Task<AnalyzeResponse> Analyze(AnalyzeRequest request, ServerCallContext context)
        {
            return WrapProviderCall(() => base.Analyze(request, context));
        }

        public override Task<AnalyzeResponse> AnalyzeStack(AnalyzeStackRequest request, ServerCallContext context)
        {
            return WrapProviderCall(() => base.AnalyzeStack(request, context));
        }

        public override Task<RemediateResponse> Remediate(AnalyzeRequest request, ServerCallContext context)
        {
            return WrapProviderCall(() => base.Remediate(request, context));
        }

        public override Task<AnalyzerInfo> GetAnalyzerInfo(Empty request, ServerCallContext context)
        {
            return WrapProviderCall(() => base.GetAnalyzerInfo(request, context));
        }

        public override Task<Empty> Configure(ConfigureAnalyzerRequest request, ServerCallContext context)
        {
            return WrapProviderCall(() => base.Configure(request, context));
        }
    }
}

using System;
using System.Net;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Pulumirpc.Codegen;
using Diagnostic = Pulumi.Codegen.Diagnostic;
using DiagnosticSeverity = Pulumi.Codegen.DiagnosticSeverity;

namespace Pulumi.Experimental.Converter
{
    public sealed class ConvertProgramRequest
    {
        public readonly string SourceDirectory;
        public readonly string TargetDirectory;
        public readonly string MapperTarget;

        public ConvertProgramRequest(string sourceDirectory, string targetDirectory, string mapperTarget)
        {
            SourceDirectory = sourceDirectory;
            TargetDirectory = targetDirectory;
            MapperTarget = mapperTarget;
        }
    }

    public sealed class ConvertProgramResponse
    {
        public IList<Diagnostic>? Diagnostics { get; set; }
        public static ConvertProgramResponse Empty => new ConvertProgramResponse();
    }

    public abstract class Converter
    {
        public virtual Task<ConvertProgramResponse> ConvertProgram(ConvertProgramRequest request)
        {
            throw new NotImplementedException($"{nameof(ConvertProgram)} is not implemented");
        }

        public static Converter CreateSimple(Func<ConvertProgramRequest, ConvertProgramResponse> convertProgram)
        {
            return new SimpleProgramConverter(request =>
            {
                var response = convertProgram(request);
                return Task.FromResult(response);
            });
        }

        public static Converter CreateSimpleAsync(Func<ConvertProgramRequest, Task<ConvertProgramResponse>> convertProgramAsync)
        {
            return new SimpleProgramConverter(convertProgramAsync);
        }

        public static async Task Serve(
            Converter converter,
            CancellationToken? cancellationToken = null,
            TextWriter? stdout = null)
        {
            // maxRpcMessageSize raises the gRPC Max message size from `4194304` (4mb) to `419430400` (400mb)
            var maxRpcMessageSize = 400 * 1024 * 1024;

            var host = Host.CreateDefaultBuilder()
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
                        })
                        .ConfigureLogging(loggingBuilder =>
                        {
                            // disable default logging
                            loggingBuilder.ClearProviders();
                        })
                        .ConfigureServices(services =>
                        {
                            services.AddSingleton(new ConverterService(converter));

                            services.AddGrpc(grpcOptions =>
                            {
                                grpcOptions.MaxReceiveMessageSize = maxRpcMessageSize;
                                grpcOptions.MaxSendMessageSize = maxRpcMessageSize;
                            });

                            services.AddGrpcReflection();
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapGrpcService<ConverterService>();
                                endpoints.MapGrpcReflectionService();
                            });
                        });
                })
                .Build();

            // before starting the host, set up this callback to tell us what port was selected
            var portTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(() =>
            {
                try
                {
                    var serverFeatures = host.Services.GetRequiredService<IServer>().Features;
                    var addressesFeature = serverFeatures.Get<IServerAddressesFeature>();
                    Debug.Assert(addressesFeature != null, "Server should have an IServerAddressesFeature");
                    var addresses = addressesFeature.Addresses.ToList();
                    Debug.Assert(addresses.Count == 1, "Server should only be listening on one address");
                    var uri = new Uri(addresses[0]);
                    portTcs.TrySetResult(uri.Port);
                }
                catch (Exception ex)
                {
                    portTcs.TrySetException(ex);
                }
            });

            var ct = cancellationToken ?? CancellationToken.None;
            stdout ??= System.Console.Out;

            await host.StartAsync(ct);

            var port = await portTcs.Task;
            // Explicitly write just the number and "\n". WriteLine would write "\r\n" on Windows, and while
            // the engine has now been fixed to handle that (see https://github.com/pulumi/pulumi/pull/11915)
            // we work around this here so that old engines can use dotnet providers as well.
            await stdout.WriteAsync(port.ToString() + "\n");

            await host.WaitForShutdownAsync(ct);

            host.Dispose();
        }
    }

    class SimpleProgramConverter : Converter
    {
        private readonly Func<ConvertProgramRequest, Task<ConvertProgramResponse>> _convertProgram;

        public SimpleProgramConverter(Func<ConvertProgramRequest, Task<ConvertProgramResponse>> convertProgram)
        {
            _convertProgram = convertProgram;
        }
        public override async Task<ConvertProgramResponse> ConvertProgram(ConvertProgramRequest request)
        {
            return await _convertProgram(request);
        }
    }

    class ConverterService : Pulumirpc.Converter.ConverterBase
    {
        private readonly Func<ConvertProgramRequest, Task<ConvertProgramResponse>> _convertProgram;

        public ConverterService(Converter implementation)
        {
            _convertProgram = implementation.ConvertProgram;
        }

        private Pulumirpc.Codegen.Range RpcRange(Pulumi.Codegen.Range range)
        {
            var rpcRange = new Pulumirpc.Codegen.Range();
            rpcRange.Filename = range.Filename ?? "";
            if (range.Start != null)
            {
                rpcRange.Start = new Pos
                {
                    Line = range.Start.Line,
                    Column = range.Start.Column,
                    Byte = range.Start.Byte
                };
            }

            if (range.End != null)
            {
                rpcRange.End = new Pos
                {
                    Line = range.End.Line,
                    Column = range.End.Column,
                    Byte = range.End.Byte
                };
            }

            return rpcRange;
        }

        public override async Task<Pulumirpc.ConvertProgramResponse> ConvertProgram(Pulumirpc.ConvertProgramRequest rpcRequest, ServerCallContext context)
        {
            var request = new ConvertProgramRequest(
                sourceDirectory: rpcRequest.SourceDirectory,
                targetDirectory: rpcRequest.TargetDirectory,
                mapperTarget: rpcRequest.MapperTarget);

            var response = await _convertProgram(request);

            if (response.Diagnostics == null)
            {
                return new Pulumirpc.ConvertProgramResponse();
            }

            var rpcResponse = new Pulumirpc.ConvertProgramResponse();
            foreach (var diagnostic in response.Diagnostics)
            {
                var rpcDiagnostic = new Pulumirpc.Codegen.Diagnostic
                {
                    Detail = diagnostic.Detail ?? "",
                    Summary = diagnostic.Summary ?? "",
                    Severity = diagnostic.Severity switch
                    {
                        DiagnosticSeverity.Warning => Pulumirpc.Codegen.DiagnosticSeverity.DiagWarning,
                        DiagnosticSeverity.Error => Pulumirpc.Codegen.DiagnosticSeverity.DiagError,
                        _ => Pulumirpc.Codegen.DiagnosticSeverity.DiagInvalid
                    }
                };

                if (diagnostic.Subject != null)
                {
                    rpcDiagnostic.Subject = RpcRange(diagnostic.Subject);
                }

                if (diagnostic.Context != null)
                {
                    rpcDiagnostic.Context = RpcRange(diagnostic.Context);
                }

                rpcResponse.Diagnostics.Add(rpcDiagnostic);
            }

            return rpcResponse;
        }
    }
}

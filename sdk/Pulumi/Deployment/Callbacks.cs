// Copyright 2016-2024, Pulumi Corporation

using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;

namespace Pulumi
{
    /// <summary>
    /// A callback function that can be invoked by the engine to perform some operation. The input message will be a
    /// byte serialized protobuf message, which the callback function should deserialize and process. The return value
    /// is a protobuf message that the SDK will serialize and return to the engine.
    /// </summary>
    /// <param name="message">A byte serialized protobuf message.</param>
    /// <param name="cancellationToken">The async cancellation token.</param>
    /// <returns>A protobuf message to be returned to the engine.</returns>
    internal delegate Task<IMessage> Callback(ByteString message, CancellationToken cancellationToken = default);

    /// <summary>
    /// This class implements the callbacks server used by the engine to invoke remote functions in the dotnet process.
    /// </summary>
    internal sealed class Callbacks : Pulumirpc.Callbacks.CallbacksBase
    {
        private readonly ConcurrentDictionary<string, Callback> _callbacks = new ConcurrentDictionary<string, Callback>();
        private readonly Task<string> _target;

        public Callbacks(Task<string> target)
        {
            _target = target;
        }

        public async Task<Pulumirpc.Callback> AllocateCallback(Callback callback)
        {
            // Find a unique token for this callback, this will generally succed on first attempt.
            var token = Guid.NewGuid().ToString();
            while (!_callbacks.TryAdd(token, callback))
            {
                token = Guid.NewGuid().ToString();
            }

            var result = new Pulumirpc.Callback();
            result.Token = token;
            result.Target = await _target.ConfigureAwait(false);
            return result;
        }

        public override async Task<Pulumirpc.CallbackInvokeResponse> Invoke(Pulumirpc.CallbackInvokeRequest request, ServerCallContext context)
        {
            if (!_callbacks.TryGetValue(request.Token, out var callback))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, string.Format("Callback not found: {}", request.Token)));
            }

            try
            {
                var result = await callback(request.Request, context.CancellationToken).ConfigureAwait(false);
                var response = new Pulumirpc.CallbackInvokeResponse();
                response.Response = result.ToByteString();
                return response;
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }
    }

    internal sealed class CallbacksHost : IAsyncDisposable
    {
        private readonly TaskCompletionSource<string> _targetTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly IHost _host;
        private readonly CancellationTokenRegistration _portRegistration;

        public CallbacksHost()
        {
            this._host = Host.CreateDefaultBuilder()
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
                        })
                        .ConfigureLogging(loggingBuilder =>
                        {
                            // disable default logging
                            loggingBuilder.ClearProviders();
                        })
                        .ConfigureServices(services =>
                        {
                            // Injected into Callbacks constructor
                            services.AddSingleton(new Callbacks(_targetTcs.Task));

                            services.AddGrpc(grpcOptions =>
                            {
                                // MaxRpcMesageSize raises the gRPC Max Message size from `4194304` (4mb) to `419430400` (400mb)
                                var maxRpcMesageSize = 1024 * 1024 * 400;

                                grpcOptions.MaxReceiveMessageSize = maxRpcMesageSize;
                                grpcOptions.MaxSendMessageSize = maxRpcMesageSize;
                            });
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapGrpcService<Callbacks>();
                            });
                        });
                })
                .Build();

            // before starting the host, set up this callback to tell us what port was selected
            this._portRegistration = this._host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(() =>
            {
                try
                {
                    var serverFeatures = this._host.Services.GetRequiredService<IServer>().Features;
                    // Server should only be listening on one address
                    var address = serverFeatures.Get<IServerAddressesFeature>()!.Addresses.Single();
                    var uri = new Uri(address);
                    // grpc expects just hostname:port, not http://hostname:port
                    var target = $"{uri.Host}:{uri.Port}";
                    this._targetTcs.TrySetResult(target);
                }
                catch (Exception ex)
                {
                    this._targetTcs.TrySetException(ex);
                }
            });
        }

        public Callbacks Callbacks => this._host.Services.GetRequiredService<Callbacks>();

        public Task StartAsync(CancellationToken cancellationToken)
            => this._host.StartAsync(cancellationToken);

        public async ValueTask DisposeAsync()
        {
            this._portRegistration.Unregister();
            await this._host.StopAsync().ConfigureAwait(false);
            this._host.Dispose();
        }
    }
}

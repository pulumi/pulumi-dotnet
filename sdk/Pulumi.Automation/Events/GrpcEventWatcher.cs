// Copyright 2016-2025, Pulumi Corporation

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Pulumi.Automation.Serialization;
using Pulumirpc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Pulumi.Automation.Events
{
    /// <summary>
    /// gRPC-based event watcher that hosts a gRPC server to receive engine events.
    /// </summary>
    internal sealed class GrpcEventWatcher : IEventWatcher
    {
        private readonly IHost _host;
        private readonly TaskCompletionSource<int> _portTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource _shutdownCts = new CancellationTokenSource();

        public string EventLogPath { get; private set; } = string.Empty;

        public GrpcEventWatcher(Action<EngineEvent> onEvent, CancellationToken cancellationToken)
        {
            _host = Host.CreateDefaultBuilder()
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
                            services.AddGrpc(grpcOptions =>
                            {
                                grpcOptions.MaxReceiveMessageSize = 1024 * 1024 * 4; // 4MB
                                grpcOptions.MaxSendMessageSize = 1024 * 1024 * 4; // 4MB
                            });
                            services.AddSingleton(new EventsServer(onEvent));
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapGrpcService<EventsServer>();
                            });
                        });
                })
                .Build();

            // Set up callback to capture the port once the server starts
            _host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(() =>
            {
                try
                {
                    var serverFeatures = _host.Services.GetRequiredService<IServer>().Features;
                    var addresses = serverFeatures.Get<IServerAddressesFeature>()!.Addresses.ToList();
                    var uri = new Uri(addresses[0]);
                    var port = uri.Port;
                    EventLogPath = $"tcp://localhost:{port}";
                    _portTcs.TrySetResult(port);
                }
                catch (Exception ex)
                {
                    _portTcs.TrySetException(ex);
                }
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    await _host.StartAsync(_shutdownCts.Token).ConfigureAwait(false);
                    await _host.WaitForShutdownAsync(_shutdownCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }, cancellationToken);

            _ = _portTcs.Task.Wait(TimeSpan.FromSeconds(10), cancellationToken);
        }

        public async Task StopAsync()
        {
            _shutdownCts.Cancel();
            await _host.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            _shutdownCts.Cancel();
            _shutdownCts.Dispose();
            _host.Dispose();
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// gRPC service implementation for receiving engine events over gRPC.
    /// This service receives events streamed from the Pulumi CLI during stack operations.
    /// </summary>
    internal sealed class EventsServer : Pulumirpc.Events.EventsBase
    {
        private readonly LocalSerializer _localSerializer = new LocalSerializer();
        private readonly Action<EngineEvent> _onEvent;

        public EventsServer(Action<EngineEvent> onEvent)
        {
            _onEvent = onEvent;
        }

        public override async Task<Empty> StreamEvents(IAsyncStreamReader<EventRequest> requestStream, ServerCallContext context)
        {
            try
            {
                while (await requestStream.MoveNext(context.CancellationToken).ConfigureAwait(false))
                {
                    var request = requestStream.Current;
                    var eventJson = request.Event;

                    if (!string.IsNullOrWhiteSpace(eventJson) && _localSerializer.IsValidJson(eventJson))
                    {
                        var engineEvent = _localSerializer.DeserializeJson<EngineEvent>(eventJson);
                        _onEvent.Invoke(engineEvent);
                    }
                }
            }
            catch (Exception) when (context.CancellationToken.IsCancellationRequested)
            {
                // Operation was cancelled, this is expected
            }
            catch (Exception)
            {
                // Log or handle other exceptions if needed
                throw;
            }

            return new Empty();
        }
    }
}

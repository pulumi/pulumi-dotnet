// Copyright 2016-2020, Pulumi Corporation

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Pulumirpc;

namespace Pulumi
{
    internal class GrpcEngine : Experimental.IEngine
    {
        private readonly Engine.EngineClient _engine;
        // Using a static dictionary to keep track of and re-use gRPC channels
        // According to the docs (https://docs.microsoft.com/en-us/aspnet/core/grpc/performance?view=aspnetcore-6.0#reuse-grpc-channels), creating GrpcChannels is expensive so we keep track of a bunch of them here
        private static readonly ConcurrentDictionary<string, GrpcChannel> _engineChannels = new ConcurrentDictionary<string, GrpcChannel>();
        private static readonly object _channelsLock = new object();
        public GrpcEngine(string engineAddress)
        {
            // maxRpcMessageSize raises the gRPC Max Message size from `4194304` (4mb) to `419430400` (400mb)
            const int maxRpcMessageSize = 400 * 1024 * 1024;
            if (_engineChannels.TryGetValue(engineAddress, out var engineChannel))
            {
                // A channel already exists for this address
                this._engine = new Engine.EngineClient(engineChannel);
            }
            else
            {
                lock (_channelsLock)
                {
                    if (_engineChannels.TryGetValue(engineAddress, out var existingChannel))
                    {
                        // A channel already exists for this address
                        this._engine = new Engine.EngineClient(existingChannel);
                    }
                    else
                    {
                        // Inititialize the engine channel once for this address
                        var channel = GrpcChannel.ForAddress(new Uri($"http://{engineAddress}"), new GrpcChannelOptions
                        {
                            MaxReceiveMessageSize = maxRpcMessageSize,
                            MaxSendMessageSize = maxRpcMessageSize,
                            Credentials = Grpc.Core.ChannelCredentials.Insecure,
                        });

                        _engineChannels[engineAddress] = channel;
                        this._engine = new Engine.EngineClient(channel);
                    }
                }
            }
        }

        public async Task LogAsync(Experimental.LogRequest request)
        {
            var rpcRequest = new Pulumirpc.LogRequest();
            rpcRequest.Message = request.Message;
            rpcRequest.Ephemeral = request.Ephemeral;
            rpcRequest.Urn = request.Urn == null ? "" : request.Urn;
            rpcRequest.Severity = (Pulumirpc.LogSeverity)request.Severity;
            rpcRequest.StreamId = request.StreamId;
            await this._engine.LogAsync(rpcRequest);
        }
    }
}

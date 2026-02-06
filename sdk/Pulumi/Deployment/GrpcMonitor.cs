// Copyright 2016-2020, Pulumi Corporation

using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Pulumirpc;
using Grpc.Core;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Pulumi
{
    internal class GrpcMonitor : IMonitor
    {
        private readonly ResourceMonitor.ResourceMonitorClient _client;

        // Using a static dictionary to keep track of and re-use gRPC channels
        // According to the docs (https://docs.microsoft.com/en-us/aspnet/core/grpc/performance?view=aspnetcore-6.0#reuse-grpc-channels), creating GrpcChannels is expensive so we keep track of a bunch of them here
        private static readonly ConcurrentDictionary<string, Lazy<GrpcChannel>> _monitorChannels = new();

        public GrpcMonitor(string monitorAddress)
        {
            var monitorChannel = _monitorChannels.GetOrAdd(monitorAddress, LazyCreateChannel);
            this._client = new ResourceMonitor.ResourceMonitorClient(monitorChannel.Value);
        }

        private static Lazy<GrpcChannel> LazyCreateChannel(string monitorAddress)
        {
            return new Lazy<GrpcChannel>(() => CreateChannel(monitorAddress));
        }

        private static GrpcChannel CreateChannel(string monitorAddress)
        {
            const int maxRpcMessageSize = 400 * 1024 * 1024;
            var channel = GrpcChannel.ForAddress(new Uri($"http://{monitorAddress}"), new GrpcChannelOptions
            {
                MaxReceiveMessageSize = maxRpcMessageSize,
                MaxSendMessageSize = maxRpcMessageSize,
                Credentials = ChannelCredentials.Insecure
            });
            return channel;
        }

        public async Task<SupportsFeatureResponse> SupportsFeatureAsync(SupportsFeatureRequest request)
            => await this._client.SupportsFeatureAsync(request);

        public async Task<InvokeResponse> InvokeAsync(ResourceInvokeRequest request)
            => await this._client.InvokeAsync(request);

        public async Task<CallResponse> CallAsync(ResourceCallRequest request)
            => await this._client.CallAsync(request);

        public async Task<RegisterPackageResponse> RegisterPackageAsync(Pulumirpc.RegisterPackageRequest request)
            => await this._client.RegisterPackageAsync(request);

        public async Task<ReadResourceResponse> ReadResourceAsync(Resource resource, ReadResourceRequest request)
            => await this._client.ReadResourceAsync(request);

        public async Task<RegisterResourceResponse> RegisterResourceAsync(Resource resource, RegisterResourceRequest request)
            => await this._client.RegisterResourceAsync(request);

        public async Task RegisterResourceOutputsAsync(RegisterResourceOutputsRequest request)
            => await this._client.RegisterResourceOutputsAsync(request);

        public async Task RegisterStackInvokeTransform(Pulumirpc.Callback callback)
            => await this._client.RegisterStackInvokeTransformAsync(callback);

        public async Task RegisterResourceHookAsync(RegisterResourceHookRequest request)
            => await this._client.RegisterResourceHookAsync(request);

        public async Task RegisterErrorHookAsync(RegisterErrorHookRequest request)
            => await this._client.RegisterErrorHookAsync(request);

        public async Task SignalAndWaitForShutdownAsync()
        {
            try
            {
                await this._client.SignalAndWaitForShutdownAsync(new Google.Protobuf.WellKnownTypes.Empty());
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.Unimplemented)
            {
                // If we are running against an older version of the CLI, SignalAndWaitForShutdown might not be
                // implemented. This is mostly fine, but means that delete hooks do not work. Since we check if
                // the CLI supports the `resourceHook` feature when registering hooks, it's fine to ignore the
                // `UNIMPLEMENTED` error here.
                return;
            }
        }
    }
}

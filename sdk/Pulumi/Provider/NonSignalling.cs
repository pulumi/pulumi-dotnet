// Copyright 2025, Pulumi Corporation

using System;
using System.Threading.Tasks;
using Pulumirpc;

namespace Pulumi.Experimental.Provider
{
    /// <summary>
    /// NonSignallingDeploymentBuilder decorates an existing <see cref="IDeploymentBuilder"/> to create an <see
    /// cref="IMonitor"/> that does not signal the engine for shutdown. This is useful in scenarios where we want to
    /// kick off a deployment in tandem with another that is already responsible for managing the engine's lifecycle,
    /// such as when a provider <c>Construct</c>s a component resource. In such cases, we do not want to signal shutdown
    /// at the end of the <c>Construct</c> operation, as the engine is still in use by the program that created the
    /// component.
    /// </summary>
    internal class NonSignallingDeploymentBuilder : IDeploymentBuilder
    {
        /// <summary>
        /// The <see cref="IDeploymentBuilder"/> that this instance decorates.
        /// </summary>
        private IDeploymentBuilder DeploymentBuilder { get; }

        /// <summary>
        /// Initializes a new <see cref="NonSignallingDeploymentBuilder"/>.
        /// </summary>
        public NonSignallingDeploymentBuilder(IDeploymentBuilder deploymentBuilder)
        {
            DeploymentBuilder = deploymentBuilder ?? throw new ArgumentNullException(nameof(deploymentBuilder));
        }

        public IEngine BuildEngine(string engineAddress)
          => DeploymentBuilder.BuildEngine(engineAddress);

        public IMonitor BuildMonitor(string monitoringEndpoint)
          => new NonSignallingMonitor(DeploymentBuilder.BuildMonitor(monitoringEndpoint));
    }

    /// <summary>
    /// NonSignallingMonitor decorates an existing <see cref="IMonitor"/> to not signal the engine for shutdown. This
    /// is useful in scenarios where we know that another monitor is responsible for managing the engine's
    /// lifecycle, such as when a provider <c>Construct</c>s a component resource.
    /// </summary>
    internal class NonSignallingMonitor : IMonitor
    {
        /// <summary>
        /// The <see cref="IMonitor"/> that this instance decorates.
        /// </summary>
        private IMonitor Monitor { get; }

        /// <summary>
        /// Initializes a new <see cref="NonSignallingMonitor"/>.
        /// </summary>
        public NonSignallingMonitor(IMonitor monitor)
        {
            Monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        }

        public async Task<SupportsFeatureResponse> SupportsFeatureAsync(SupportsFeatureRequest request)
            => await Monitor.SupportsFeatureAsync(request);

        public async Task<Pulumirpc.InvokeResponse> InvokeAsync(ResourceInvokeRequest request)
            => await Monitor.InvokeAsync(request);

        public async Task<Pulumirpc.CallResponse> CallAsync(ResourceCallRequest request)
            => await Monitor.CallAsync(request);

        public async Task<RegisterPackageResponse> RegisterPackageAsync(Pulumirpc.RegisterPackageRequest request)
            => await Monitor.RegisterPackageAsync(request);

        public async Task<ReadResourceResponse> ReadResourceAsync(Resource resource, ReadResourceRequest request)
            => await Monitor.ReadResourceAsync(resource, request);

        public async Task<RegisterResourceResponse> RegisterResourceAsync(Resource resource, RegisterResourceRequest request)
            => await Monitor.RegisterResourceAsync(resource, request);

        public async Task RegisterResourceOutputsAsync(RegisterResourceOutputsRequest request)
            => await Monitor.RegisterResourceOutputsAsync(request);

        public async Task RegisterStackInvokeTransform(Pulumirpc.Callback callback)
            => await Monitor.RegisterStackInvokeTransform(callback);

        public async Task RegisterResourceHookAsync(RegisterResourceHookRequest request)
            => await Monitor.RegisterResourceHookAsync(request);

        public Task SignalAndWaitForShutdownAsync() => Task.CompletedTask;
    }
}

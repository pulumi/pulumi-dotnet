// Copyright 2016-2020, Pulumi Corporation

using System.Threading.Tasks;
using Pulumirpc;

namespace Pulumi
{
    internal interface IMonitor
    {
        Task<SupportsFeatureResponse> SupportsFeatureAsync(SupportsFeatureRequest request);

        Task<InvokeResponse> InvokeAsync(ResourceInvokeRequest request);

        Task RegisterStackInvokeTransform(Pulumirpc.Callback callback);

        Task<CallResponse> CallAsync(ResourceCallRequest request);

        Task<RegisterPackageResponse> RegisterPackageAsync(Pulumirpc.RegisterPackageRequest request);

        Task<ReadResourceResponse> ReadResourceAsync(Resource resource, ReadResourceRequest request);

        Task<RegisterResourceResponse> RegisterResourceAsync(Resource resource, RegisterResourceRequest request);

        Task RegisterResourceOutputsAsync(RegisterResourceOutputsRequest request);

        /// <summary>
        /// Registers a resource hook against the deployment, returning a task that completes when the registration
        /// has finished.
        /// </summary>
        Task RegisterResourceHookAsync(RegisterResourceHookRequest request);

        /// <summary>
        /// Registers an error hook against the deployment, returning a task that completes when the registration
        /// has finished.
        /// </summary>
        Task RegisterErrorHookAsync(RegisterErrorHookRequest request);

        /// <summary>
        /// Signals to the monitor that no more resources will be registered and that the program has no more work to
        /// do. This method should not return until the deployment is finished and it is safe for the program to exit
        /// (e.g. the engine does not need anything more from the program, such as resource hook implementations).
        /// </summary>
        Task SignalAndWaitForShutdownAsync();
    }
}

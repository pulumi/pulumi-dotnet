// Copyright 2016-2026, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pulumi
{
    internal interface IDeploymentInternal : IDeployment
    {
        string? GetConfig(string fullKey);
        bool IsConfigSecret(string fullKey);

        Stack Stack { get; set; }

        IEngineLogger Logger { get; }
        IRunner Runner { get; }
        Experimental.IEngine Engine { get; }

        void ReadOrRegisterResource(
            Resource resource, bool remote, Func<string, Resource> newDependency, ResourceArgs args,
            ResourceOptions opts,
            RegisterPackageRequest? registerPackageRequest = null);
        void RegisterResourceOutputs(Resource resource, Output<IDictionary<string, object?>> outputs);

        /// <summary>
        /// Registers a resource hook against the deployment, returning a task that completes when the registration
        /// has finished.
        /// </summary>
        Task RegisterResourceHook(ResourceHook hook);

        /// <summary>
        /// Signals to the deployment that no more resources will be registered and that the program has no more work to
        /// do. This method should not return until the deployment is finished and it is safe for the program to exit
        /// (e.g. the engine does not need anything more from the program, such as resource hook implementations).
        /// </summary>
        Task SignalAndWaitForShutdownAsync();
    }
}

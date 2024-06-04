// Copyright 2024-2024, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Pulumirpc;

namespace Pulumi
{
    public partial class Deployment
    {
        async Task<CreateNewContextResponse> IDeploymentInternal.NewContext(
            CreateNewContextRequest request
        )
        {
            return await this.Monitor.CreateNewContextAsync(request).ConfigureAwait(false);
        }


        public static async Task<IDictionary<string, object?>> WithDefaultProviders(
        Func<IDictionary<string, object?>> func,
                params ProviderResource[] providers
            )
        {
            var providerRefs = new List<string>();
            foreach (var provider in providers)
            {
                var providerRef = await ProviderResource
                    .RegisterAsync(provider)
                    .ConfigureAwait(false);
                providerRefs.Add(providerRef ?? "");
            }

            var request = new CreateNewContextRequest();
            request.Providers.AddRange(providerRefs);

            var result = await Deployment
                .InternalInstance.NewContext(request)
                .ConfigureAwait(false);

            var currentDeployment = Deployment.InternalInstance;
            Environment.SetEnvironmentVariable("PULUMI_MONITOR", result.MonitorTarget);
            var deployment = new Deployment();
            Instance = new DeploymentInstance(deployment);
            Instance.Internal.Stack = currentDeployment.Stack;
            return func();
        }
    }
}

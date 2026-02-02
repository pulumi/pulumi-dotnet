// Copyright 2016-2026, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Pulumi.Serialization;
using Pulumirpc;

namespace Pulumi
{
    public sealed partial class Deployment
    {
        private class RequirePulumiVersionException : Exception
        {
            public RequirePulumiVersionException(string error)
                : base(error)
            {
            }
        }

        public static Task RequirePulumiVersionAsync(string pulumiVersionRange)
        {
            var task = RequirePulumiVersionImplAsync(InternalInstance, pulumiVersionRange);
            InternalInstance.Runner.RegisterTask($"RequirePulumiVersion({pulumiVersionRange})", task);
            return task;
        }

        private static async Task RequirePulumiVersionImplAsync(IDeploymentInternal deployment, string pulumiVersionRange)
        {
            try
            {
                await deployment.Engine.RequirePulumiVersionAsync(pulumiVersionRange).ConfigureAwait(false);
            }
            catch (Grpc.Core.RpcException ex)
            {
                if (ex.StatusCode == Grpc.Core.StatusCode.Unimplemented)
                {
                    throw new RequirePulumiVersionException(
                        "The installed version of the CLI does not support the `RequirePulumiVersion` RPC. Please upgrade the Pulumi CLI");
                }
                throw new RequirePulumiVersionException(ex.Status.Detail);
            }
        }
    }
}

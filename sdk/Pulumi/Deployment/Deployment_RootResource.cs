// Copyright 2016-2021, Pulumi Corporation

using System;
using System.Threading.Tasks;

namespace Pulumi
{
    public partial class Deployment
    {
        /// <summary>
        /// Returns a root resource URN that will automatically become the default parent of all
        /// resources.  This can be used to ensure that all resources without explicit parents are
        /// parented to a common parent resource.
        /// </summary>
        /// <returns></returns>
        internal static async Task<string?> GetRootResourceAsync(string type)
        {
            // If we're calling this while creating the stack itself.  No way to know its urn at
            // this point.
            if (type == Stack._rootPulumiStackTypeName)
                return null;

            var resUrn = await InternalInstance.Stack.Urn.GetValueAsync(whenUnknown: default!)
                .ConfigureAwait(false);
            return resUrn;
        }
    }
}

// Copyright 2016-2026, Pulumi Corporation

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Pulumi.Serialization;
using Pulumirpc;

namespace Pulumi
{
    public sealed partial class Deployment
    {
        /// <summary>
        /// Checks with the engine whether a resource with the given type and ID exists.
        /// </summary>
        /// <param name="type">The resource type token (e.g. <c>"aws:s3/bucket:Bucket"</c>).</param>
        /// <param name="id">The provider-assigned ID of the resource to check.</param>
        /// <param name="state">Optional state sufficient to uniquely identify the resource for providers that need it.</param>
        /// <returns>An <see cref="Output{T}"/> that resolves to <c>true</c> when the resource exists, <c>false</c> when it
        /// does not, and unknown when the engine could not determine existence (e.g. during a preview whose dependencies
        /// are still pending).</returns>
        public static Output<bool> ExistsResource(
            string type, Input<string> id, IDictionary<string, object?>? state = null)
            => InternalInstance.ExistsResourceInternal(type, id, state);

        Output<bool> IDeploymentInternal.ExistsResourceInternal(
            string type, Input<string> id, IDictionary<string, object?>? state)
        {
            async Task<OutputData<bool>> RunAsync()
            {
                var idData = await id.ToOutput().DataTask.ConfigureAwait(false);
                if (!idData.IsKnown)
                {
                    return new OutputData<bool>(
                        ImmutableHashSet<Resource>.Empty, default, isKnown: false, isSecret: idData.IsSecret);
                }

                var keepResources = await MonitorSupportsResourceReferences().ConfigureAwait(false);
                Google.Protobuf.WellKnownTypes.Struct? properties = null;
                if (state != null)
                {
                    var serializer = new Serializer(_excessiveDebugOutput);
                    var serialized = await serializer.SerializeAsync(
                        $"exists:{type}", state, keepResources, keepOutputValues: false).ConfigureAwait(false);
                    properties = Serializer.CreateStruct(((IDictionary<string, object?>)serialized!).ToImmutableDictionary());
                }

                var request = new ExistsResourceRequest
                {
                    Type = type,
                    Id = idData.Value ?? "",
                    Properties = properties,
                };

                var response = await Monitor.ExistsResourceAsync(request).ConfigureAwait(false);
                if (response.Unknown)
                {
                    return new OutputData<bool>(
                        ImmutableHashSet<Resource>.Empty, default, isKnown: false, isSecret: idData.IsSecret);
                }
                return new OutputData<bool>(
                    ImmutableHashSet<Resource>.Empty, response.Exists, isKnown: true, isSecret: idData.IsSecret);
            }

            return new Output<bool>(RunAsync());
        }
    }
}

// Copyright 2016-2019, Pulumi Corporation

using System.Collections.Immutable;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Pulumirpc;

namespace Pulumi
{
    public partial class Deployment
    {
        private async Task<(string urn, string id, Struct data, ImmutableDictionary<string, ImmutableHashSet<Resource>> dependencies, Pulumirpc.Result result)> ReadResourceAsync(
            Resource resource, string id, ResourceArgs args, ResourceOptions options, RegisterPackageRequest? registerPackageRequest = null)
        {
            var name = resource.GetResourceName();
            var type = resource.GetResourceType();
            var label = $"resource:{name}[{type}]#...";
            Log.Debug($"Reading resource: id={id}, t=${type}, name=${name}");

            var prepareResult = await this.PrepareResourceAsync(
                label, resource, custom: true, remote: false, args, options, registerPackageRequest).ConfigureAwait(false);

            Log.Debug($"ReadResource RPC prepared: id={id}, t={type}, name={name}" +
                (_excessiveDebugOutput ? $", obj={prepareResult.SerializedProps}" : ""));

            // Create a resource request and do the RPC.
            var request = new ReadResourceRequest
            {
                Type = type,
                Name = name,
                Id = id,
                Parent = prepareResult.ParentUrn,
                Provider = prepareResult.ProviderRef,
                Properties = prepareResult.SerializedProps,
                Version = options.Version ?? "",
                AcceptSecrets = true,
                AcceptResources = !_disableResourceReferences,
            };

            if (prepareResult.PackageRef != null)
            {
                request.PackageRef = prepareResult.PackageRef;
            }

            request.Dependencies.AddRange(prepareResult.AllDirectDependencyUrns);

            // Now run the operation, serializing the invocation if necessary.
            var response = await this.Monitor.ReadResourceAsync(resource, request).ConfigureAwait(false);

            return (response.Urn, id, response.Properties, ImmutableDictionary<string, ImmutableHashSet<Resource>>.Empty, Pulumirpc.Result.Success);
        }
    }
}

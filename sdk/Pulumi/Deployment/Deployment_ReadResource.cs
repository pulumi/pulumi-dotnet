// Copyright 2016-2019, Pulumi Corporation

using System.Collections.Immutable;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Pulumi.Serialization;
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
                label, resource, custom: true, remote: false, args, options, read: true, registerPackageRequest).ConfigureAwait(false);

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

            // Prefer the id that the provider's Read resolved. An empty response id is unknown during
            // preview. During an update, it comes from an older engine that does not set the field, so
            // fall back to the request id. An empty string resolves the id as unknown.
            var responseId = response.Id != "" ? response.Id
                : _isDryRun || id == Constants.UnknownValue ? "" : id;
            // The engine reports a skipped read as a successful result with Unknown set. The engine
            // reports a skipped registration as FAIL, so do the same here to resolve the outputs as unknown.
            var result = response.Unknown ? Pulumirpc.Result.Fail : response.Result;
            return (response.Urn, responseId, response.Properties, ImmutableDictionary<string, ImmutableHashSet<Resource>>.Empty, result);
        }
    }
}

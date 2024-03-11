// Copyright 2016-2024, Pulumi Corporation

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Pulumirpc;

namespace Pulumi
{
    public partial class Deployment
    {
        private async Task<(string urn, string id, Struct data, ImmutableDictionary<string, ImmutableHashSet<Resource>> dependencies)> RegisterResourceAsync(
            Resource resource, bool remote, Func<string, Resource> newDependency, ResourceArgs args,
            ResourceOptions options)
        {
            var name = resource.GetResourceName();
            var type = resource.GetResourceType();
            var custom = resource is CustomResource;

            var label = $"resource:{name}[{type}]";
            Log.Debug($"Registering resource start: t={type}, name={name}, custom={custom}, remote={remote}");

            if (options.DeletedWith != null && !(await MonitorSupportsDeletedWith().ConfigureAwait(false)))
            {
                throw new Exception("The Pulumi CLI does not support the DeletedWith option. Please update the Pulumi CLI.");
            }

            var request = await CreateRegisterResourceRequest(type, name, custom, remote, options);

            Log.Debug($"Preparing resource: t={type}, name={name}, custom={custom}, remote={remote}");
            var prepareResult = await PrepareResourceAsync(label, resource, custom, remote, args, options).ConfigureAwait(false);
            Log.Debug($"Prepared resource: t={type}, name={name}, custom={custom}, remote={remote}");

            PopulateRequest(request, prepareResult);

            Log.Debug($"Registering resource monitor start: t={type}, name={name}, custom={custom}, remote={remote}");
            var result = await this.Monitor.RegisterResourceAsync(resource, request).ConfigureAwait(false);
            Log.Debug($"Registering resource monitor end: t={type}, name={name}, custom={custom}, remote={remote}");

            var dependencies = ImmutableDictionary.CreateBuilder<string, ImmutableHashSet<Resource>>();
            foreach (var (key, propertyDependencies) in result.PropertyDependencies)
            {
                var urns = ImmutableHashSet.CreateBuilder<Resource>();
                foreach (var urn in propertyDependencies.Urns)
                {
                    urns.Add(newDependency(urn));
                }
                dependencies[key] = urns.ToImmutable();
            }

            return (result.Urn, result.Id, result.Object, dependencies.ToImmutable());
        }

        private static void PopulateRequest(RegisterResourceRequest request, PrepareResult prepareResult)
        {
            request.Object = prepareResult.SerializedProps;
            request.Parent = prepareResult.ParentUrn;
            request.Provider = prepareResult.ProviderRef;
            request.Providers.Add(prepareResult.ProviderRefs);
            if (prepareResult.SupportsAliasSpec)
            {
                request.Aliases.AddRange(prepareResult.Aliases);
            }
            else
            {
                var aliasUrns = prepareResult.Aliases.Select(a => a.Urn);
                request.AliasURNs.AddRange(aliasUrns);
            }

            request.Transforms.AddRange(prepareResult.Transforms);
            request.Dependencies.AddRange(prepareResult.AllDirectDependencyUrns);

            foreach (var (key, resourceUrns) in prepareResult.PropertyToDirectDependencyUrns)
            {
                var deps = new RegisterResourceRequest.Types.PropertyDependencies();
                deps.Urns.AddRange(resourceUrns);
                request.PropertyDependencies.Add(key, deps);
            }
        }

        private static async Task<RegisterResourceRequest> CreateRegisterResourceRequest(
            string type, string name, bool custom, bool remote, ResourceOptions options)
        {
            var customOpts = options as CustomResourceOptions;
            var deleteBeforeReplace = customOpts?.DeleteBeforeReplace;
            var deletedWith = "";
            if (options.DeletedWith != null)
            {
                deletedWith = await options.DeletedWith.Urn.GetValueAsync("").ConfigureAwait(false);
            }

            var request = new RegisterResourceRequest
            {
                Type = type,
                Name = name,
                Custom = custom,
                Protect = options.Protect ?? false,
                Version = options.Version ?? "",
                PluginDownloadURL = options.PluginDownloadURL ?? "",
                ImportId = customOpts?.ImportId ?? "",
                AcceptSecrets = true,
                AcceptResources = !_disableResourceReferences,
                DeleteBeforeReplace = deleteBeforeReplace ?? false,
                DeleteBeforeReplaceDefined = deleteBeforeReplace != null,
                CustomTimeouts = options.CustomTimeouts?.Serialize(),
                Remote = remote,
                RetainOnDelete = options.RetainOnDelete ?? false,
                DeletedWith = deletedWith,
            };

            if (customOpts != null)
            {
                request.AdditionalSecretOutputs.AddRange(customOpts.AdditionalSecretOutputs);
                request.ReplaceOnChanges.AddRange(customOpts.ReplaceOnChanges);
            }

            request.IgnoreChanges.AddRange(options.IgnoreChanges);

            return request;
        }
    }
}

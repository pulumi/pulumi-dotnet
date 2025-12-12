// Copyright 2016-2024, Pulumi Corporation

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
    public partial class Deployment
    {
        private async Task<(string urn, string id, Struct data, ImmutableDictionary<string, ImmutableHashSet<Resource>> dependencies, Pulumirpc.Result result)> RegisterResourceAsync(
            Resource resource, bool remote, Func<string, Resource> newDependency, ResourceArgs args,
            ResourceOptions options,
            RegisterPackageRequest? registerPackageRequest = null)
        {
            var name = resource.GetResourceName();
            var type = resource.GetResourceType();
            var custom = resource is CustomResource;

            var label = $"resource:{name}[{type}]";
            Log.Debug($"Registering resource start: t={type}, name={name}, custom={custom}, remote={remote}");

            if (options.DeletedWith != null && !(await MonitorSupportsDeletedWith().ConfigureAwait(false)))
            {
                throw new InvalidOperationException("The Pulumi CLI does not support the DeletedWith option. Please update the Pulumi CLI.");
            }

            // InputList has no IsEmpty/Count
            var hasReplaceWith = false;
            await foreach (var _ in options.ReplaceWith)
            {
                hasReplaceWith = true;
                break;
            }

            if (hasReplaceWith && !(await MonitorSupportsReplaceWith().ConfigureAwait(false)))
            {
                throw new InvalidOperationException("The Pulumi CLI does not support the ReplaceWith option. Please update the Pulumi CLI.");
            }

            var (request, replacementTriggerDeps) = await this.CreateRegisterResourceRequest(type, name, custom, remote, options);

            Log.Debug($"Preparing resource: t={type}, name={name}, custom={custom}, remote={remote}");
            var prepareResult = await PrepareResourceAsync(label, resource, custom, remote, args, options, registerPackageRequest).ConfigureAwait(false);
            Log.Debug($"Prepared resource: t={type}, name={name}, custom={custom}, remote={remote}");

            PopulateRequest(request, prepareResult, replacementTriggerDeps);

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

            return (result.Urn, result.Id, result.Object, dependencies.ToImmutable(), result.Result);
        }

        private static void PopulateRequest(RegisterResourceRequest request, PrepareResult prepareResult, HashSet<string> replacementTriggerDeps)
        {
            request.Object = prepareResult.SerializedProps;
            request.Parent = prepareResult.ParentUrn;
            request.Provider = prepareResult.ProviderRef;
            request.Providers.Add(prepareResult.ProviderRefs);
            if (prepareResult.PackageRef != null)
            {
                request.PackageRef = prepareResult.PackageRef;
            }
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

            // Merge all dependencies: from prepareResult and replacement trigger
            var allDeps = new HashSet<string>(prepareResult.AllDirectDependencyUrns);
            allDeps.UnionWith(replacementTriggerDeps);
            request.Dependencies.AddRange(allDeps);

            request.Hooks = prepareResult.Hooks;

            foreach (var (key, resourceUrns) in prepareResult.PropertyToDirectDependencyUrns)
            {
                var deps = new RegisterResourceRequest.Types.PropertyDependencies();
                deps.Urns.AddRange(resourceUrns);
                request.PropertyDependencies.Add(key, deps);
            }
        }

        private async Task<(RegisterResourceRequest, HashSet<string>)> CreateRegisterResourceRequest(
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
                Version = options.Version ?? "",
                PluginDownloadURL = options.PluginDownloadURL ?? "",
                ImportId = customOpts?.ImportId ?? "",
                AcceptSecrets = true,
                AcceptResources = !_disableResourceReferences,
                DeleteBeforeReplaceDefined = deleteBeforeReplace != null,
                CustomTimeouts = options.CustomTimeouts?.Serialize(),
                Remote = remote,
                DeletedWith = deletedWith,
                SupportsResultReporting = true,
            };

            await foreach (var resourceInput in options.ReplaceWith)
            {
                if (resourceInput != null)
                {
                    var resource = await resourceInput.ToOutput().GetValueAsync(null!).ConfigureAwait(false);
                    if (resource != null)
                    {
                        var urn = await resource.Urn.GetValueAsync("").ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(urn))
                        {
                            request.ReplaceWith.Add(urn);
                        }
                    }
                }
            }

            var replacementTriggerDeps = new HashSet<string>();
            if (options.ReplacementTrigger != null)
            {
                var label = $"resource:{name}[{type}]";
                var keepResources = await MonitorSupportsResourceReferences().ConfigureAwait(false);
                var keepOutputValues = remote && await MonitorSupportsOutputValues().ConfigureAwait(false);
                var serializer = new Serializer(_excessiveDebugOutput);
                var serialized = await serializer.SerializeAsync($"{label}.replacementTrigger", options.ReplacementTrigger, keepResources, keepOutputValues, false).ConfigureAwait(false);
                
                var value = Serializer.CreateValue(serialized);
                request.ReplacementTrigger = value;
                
                foreach (var dep in serializer.DependentResources)
                {
                    var urn = await dep.Urn.GetValueAsync("").ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(urn))
                    {
                        replacementTriggerDeps.Add(urn);
                    }
                }
            }

            if (deleteBeforeReplace != null)
            {
                request.DeleteBeforeReplace = deleteBeforeReplace.Value;
            }
            if (options.Protect != null)
            {
                request.Protect = options.Protect.Value;
            }
            if (options.RetainOnDelete != null)
            {
                request.RetainOnDelete = options.RetainOnDelete.Value;
            }

            if (customOpts != null)
            {
                request.AdditionalSecretOutputs.AddRange(customOpts.AdditionalSecretOutputs);
                request.ReplaceOnChanges.AddRange(customOpts.ReplaceOnChanges);
            }

            request.IgnoreChanges.AddRange(options.IgnoreChanges);
            request.HideDiffs.AddRange(options.HideDiffs);

            return (request, replacementTriggerDeps);
        }
    }
}

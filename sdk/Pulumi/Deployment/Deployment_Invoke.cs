// Copyright 2016-2021, Pulumi Corporation

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
        Task IDeployment.InvokeAsync(string token, InvokeArgs args, InvokeOptions? options, RegisterPackageRequest? registerPackageRequest)
            => InvokeAsync<object>(token, args, options, convertResult: false);

        Task IDeployment.InvokeAsync(string token, InvokeArgs args, InvokeOptions? options)
            => InvokeAsync<object>(token, args, options, convertResult: false, registerPackageRequest: null);

        Task<T> IDeployment.InvokeAsync<T>(string token, InvokeArgs args, InvokeOptions? options, RegisterPackageRequest? registerPackageRequest)
            => InvokeAsync<T>(token, args, options, convertResult: true);

        Task<T> IDeployment.InvokeAsync<T>(string token, InvokeArgs args, InvokeOptions? options)
            => InvokeAsync<T>(token, args, options, convertResult: false, registerPackageRequest: null);

        async Task<T> IDeployment.InvokeSingleAsync<T>(
            string token,
            InvokeArgs args,
            InvokeOptions? options,
            RegisterPackageRequest? registerPackageRequest)
        {
            var outputs = await InvokeAsync<Dictionary<string, T>>(token, args, options, convertResult: true, registerPackageRequest);
            return outputs.Values.First();
        }

        async Task<T> IDeployment.InvokeSingleAsync<T>(string token, InvokeArgs args, InvokeOptions? options)
        {
            var outputs = await InvokeAsync<Dictionary<string, T>>(
                token: token,
                args: args,
                options: options,
                convertResult: true,
                registerPackageRequest: null);

            return outputs.Values.First();
        }

        Output<T> IDeployment.Invoke<T>(
            string token,
            InvokeArgs args,
            InvokeOptions? options,
            RegisterPackageRequest? registerPackageRequest)
            => new Output<T>(RawInvoke<T>(token, args, options, registerPackageRequest));

        Output<T> IDeployment.Invoke<T>(
            string token,
            InvokeArgs args,
            InvokeOutputOptions options,
            RegisterPackageRequest? registerPackageRequest)
            => new Output<T>(RawInvoke<T>(token, args, options, registerPackageRequest));

        Output<T> IDeployment.Invoke<T>(string token, InvokeArgs args, InvokeOptions? options)
            => new Output<T>(RawInvoke<T>(token, args, options, registerPackageRequest: null));

        Output<T> IDeployment.Invoke<T>(string token, InvokeArgs args, InvokeOutputOptions options)
        => new Output<T>(RawInvoke<T>(token, args, options, registerPackageRequest: null));


        Output<T> IDeployment.InvokeSingle<T>(
            string token,
            InvokeArgs args,
            InvokeOptions? options,
            RegisterPackageRequest? registerPackageRequest)
        {
            var outputResult = new Output<Dictionary<string, T>>(RawInvoke<Dictionary<string, T>>(token, args, options, registerPackageRequest));
            return outputResult.Apply(outputs => outputs.Values.First());
        }

        Output<T> IDeployment.InvokeSingle<T>(
            string token,
            InvokeArgs args,
            InvokeOutputOptions options,
            RegisterPackageRequest? registerPackageRequest)
        {
            var outputResult = new Output<Dictionary<string, T>>(RawInvoke<Dictionary<string, T>>(token, args, options, registerPackageRequest));
            return outputResult.Apply(outputs => outputs.Values.First());
        }

        Output<T> IDeployment.InvokeSingle<T>(string token, InvokeArgs args, InvokeOptions? options)
        {
            var outputResult = new Output<Dictionary<string, T>>(
                RawInvoke<Dictionary<string, T>>(token, args, options, registerPackageRequest: null));
            return outputResult.Apply(outputs => outputs.Values.First());
        }

        Output<T> IDeployment.InvokeSingle<T>(string token, InvokeArgs args, InvokeOutputOptions options)
        {
            var outputResult = new Output<Dictionary<string, T>>(
                RawInvoke<Dictionary<string, T>>(token, args, options, registerPackageRequest: null));
            return outputResult.Apply(outputs => outputs.Values.First());
        }

        private async Task<OutputData<T>> RawInvoke<T>(
            string token,
            InvokeArgs args,
            InvokeOptions? options,
            RegisterPackageRequest? registerPackageRequest = null)
        {
            // This method backs all `Fn.Invoke()` calls that generate
            // `Output<T>` and may include `Input<T>` values in the
            // `args`. It needs to decide which control-flow tracking
            // features are supported in the SDK and which ones in the
            // provider implementing the invoke logic.
            //
            // Current choices are:
            //
            // - any resource dependency found by a recursive
            //   traversal of `args` that awaits and inspects every
            //   `Input<T>` will always be propagated into the
            //   `Output<T>`; the provider cannot "swallow"
            //   dependencies
            //
            // - the provider is responsible for deciding whether the
            //   `Output<T>` is secret and known, and may add
            //   additional dependencies
            //
            // This means that presence of secrets or unknowns in the
            // `args` does not guarantee the result is secret or
            // unknown, which differs from Pulumi SDKs that choose to
            // implement these invokes via `Apply` (currently Go and
            // Python).
            //
            // Differences from `Call`: the `Invoke` gRPC protocol
            // does not yet support passing or returning out-of-band
            // dependencies to the provider, and in-band `Resource`
            // value support is subject to feature negotiation (see
            // `MonitorSupportsResourceReferences`). So `Call` makes
            // the provider fully responsible for depdendency
            // tracking, which is a good future direction also for
            // `Invoke`.

            var keepResources = await this.MonitorSupportsResourceReferences().ConfigureAwait(false);
            var serializedArgs = await SerializeInvokeArgs(token, args, keepResources);

            // Short-circuit actually invoking if `Unknowns` are
            // present in `args`, otherwise preview can break.
            if (Serializer.ContainsUnknowns(serializedArgs.PropertyValues))
            {
                return new OutputData<T>(resources: ImmutableHashSet<Resource>.Empty,
                                         value: default!,
                                         isKnown: false,
                                         isSecret: false);
            }

            // Resource dependencies that will be added to the Output
            var resourceDependencies = new HashSet<Resource>();
            // If we depend on any CustomResources, we need to ensure that their
            // ID is known before proceeding. If it is not known, we will return
            // an unknown result.
            // Add the dependencies from the inputs to the set of resources to wait for.
            var resourcesToWaitFor = serializedArgs.PropertyToDependentResources.Values.SelectMany(r => r).ToHashSet<Resource>();
            if (options is InvokeOutputOptions outputOptions)
            {
                var deps = outputOptions.DependsOn;
                if (deps != null)
                {
                    // The direct dependencies of the invoke.
                    var resourceList = await GatherExplicitDependenciesAsync(deps).ConfigureAwait(false);
                    resourcesToWaitFor.UnionWith(resourceList);
                    // Add the resources to the list of dependencies that we'll add to the Output
                    resourceDependencies.UnionWith(resourceList);
                }
            }
            // The expanded set of dependencies, including children of components.
            var expandedResourceList = GetAllTransitivelyReferencedResources(resourcesToWaitFor.ToHashSet());
            resourcesToWaitFor.UnionWith(expandedResourceList);
            // Ensure that all resource IDs are known before proceeding.
            foreach (var resource in resourcesToWaitFor)
            {
                // Check if it's an instance of CustomResource.
                // DependencyResources inherit from CustomResource, but they
                // don't set the id. Skip them.
                if (resource is CustomResource customResource && customResource.Id != null)
                {
                    var idData = await customResource.Id.DataTask.ConfigureAwait(false);
                    if (!idData.IsKnown)
                    {
                        return new OutputData<T>(resources: ImmutableHashSet<Resource>.Empty,
                                                 value: default!,
                                                 isKnown: false,
                                                 isSecret: false);
                    }

                }
            }

            var protoArgs = serializedArgs.ToSerializationResult();
            var result = await InvokeRawAsync(token, protoArgs, options, registerPackageRequest).ConfigureAwait(false);
            var data = Pulumi.Serialization.Converter.ConvertValue<T>(err => Log.Warn(err), $"{token} result",
                                                 new Value { StructValue = result.Serialized });
            var resources = ImmutableHashSet.CreateRange(
                result.PropertyToDependentResources.Values.SelectMany(r => r)
                .Union(data.Resources).Union(resourceDependencies));
            return new OutputData<T>(resources: resources,
                                     value: data.Value,
                                     isKnown: data.IsKnown,
                                     isSecret: data.IsSecret);
        }

        private async Task<T> InvokeAsync<T>(
            string token,
            InvokeArgs args,
            InvokeOptions? options,
            bool convertResult,
            RegisterPackageRequest? registerPackageRequest = null)
        {
            var result = await InvokeRawAsync(token, args, options, registerPackageRequest).ConfigureAwait(false);

            if (!convertResult)
            {
                return default!;
            }

            var data = Pulumi.Serialization.Converter.ConvertValue<T>(err => Log.Warn(err), $"{token} result", new Value { StructValue = result.Serialized });
            return data.Value;
        }

        private async Task<SerializationResult> InvokeRawAsync(
            string token,
            SerializationResult argsSerializationResult,
            InvokeOptions? options,
            RegisterPackageRequest? registerPackageRequest = null)
        {
            await AwaitPendingRegistrations();
            var serialized = argsSerializationResult.Serialized;

            Log.Debug($"Invoke RPC prepared: token={token}" +
                (_excessiveDebugOutput ? $", obj={serialized}" : ""));

            var provider = await ProviderResource.RegisterAsync(GetProvider(token, options)).ConfigureAwait(false);

            var invokeRequest = new ResourceInvokeRequest
            {
                Tok = token,
                Provider = provider ?? "",
                Version = options?.Version ?? "",
                Args = serialized,
                AcceptResources = !_disableResourceReferences,
                PluginDownloadURL = options?.PluginDownloadURL ?? "",
            };

            if (registerPackageRequest != null)
            {
                var packageRef = await ResolvePackageRef(registerPackageRequest).ConfigureAwait(false);
                if (packageRef != null)
                {
                    invokeRequest.PackageRef = packageRef;
                }
            }

            var result = await this.Monitor.InvokeAsync(invokeRequest).ConfigureAwait(false);

            if (result.Failures.Count > 0)
            {
                var reasons = "";
                foreach (var reason in result.Failures)
                {
                    if (reasons != "")
                    {
                        reasons += "; ";
                    }

                    reasons += $"{reason.Reason} ({reason.Property})";
                }

                throw new InvokeException($"Invoke of '{token}' failed: {reasons}");
            }

            return new SerializationResult(result.Return, argsSerializationResult.PropertyToDependentResources);
        }

        private static async Task<RawSerializationResult> SerializeInvokeArgs(string token, InvokeArgs args, bool keepResources)
        {
            Log.Debug($"Invoking function: token={token} asynchronously");

            // Be resilient to misbehaving callers.
            // ReSharper disable once ConstantNullCoalescingCondition
            args ??= InvokeArgs.Empty;

            // Wait for all values to be available.
            var argsDict = await args.ToDictionaryAsync().ConfigureAwait(false);

            return await SerializeFilteredPropertiesRawAsync(
                label: $"invoke:{token}",
                args: argsDict,
                acceptKey: key => true,
                keepResources: keepResources,
                keepOutputValues: false
            ).ConfigureAwait(false);
        }

        private async Task<SerializationResult> InvokeRawAsync(
            string token,
            InvokeArgs args,
            InvokeOptions? options,
            RegisterPackageRequest? registerPackageRequest = null)
        {
            var keepResources = await this.MonitorSupportsResourceReferences().ConfigureAwait(false);
            var argsSerializationRawResult = await SerializeInvokeArgs(token, args, keepResources);
            var argsSerializationResult = argsSerializationRawResult.ToSerializationResult();
            return await InvokeRawAsync(token, argsSerializationResult, options, registerPackageRequest);
        }

        private static ProviderResource? GetProvider(string token, InvokeOptions? options)
                => options?.Provider ?? options?.Parent?.GetProvider(token);

        private class InvokeException : Exception
        {
            public InvokeException(string error)
                : base(error)
            {
            }
        }
    }
}

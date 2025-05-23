// Copyright 2016-2021, Pulumi Corporation

using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Pulumi.Serialization;
using Pulumirpc;

namespace Pulumi
{
    public sealed partial class Deployment
    {
        internal const string SelfArg = "__self__";

        void IDeployment.Call(
            string token,
            CallArgs args,
            Resource? self,
            CallOptions? options,
            RegisterPackageRequest? registerPackageRequest)
            => Call<object>(token, args, self, options, convertResult: false, registerPackageRequest);

        void IDeployment.Call(
            string token,
            CallArgs args,
            Resource? self,
            CallOptions? options)
            => Call<object>(token, args, self, options, convertResult: false, registerPackageRequest: null);

        Output<T> IDeployment.Call<T>(
            string token,
            CallArgs args,
            Resource? self,
            CallOptions? options,
            RegisterPackageRequest? registerPackageRequest)
            => Call<T>(token, args, self, options, convertResult: true, registerPackageRequest);

        Output<T> IDeployment.Call<T>(
            string token,
            CallArgs args,
            Resource? self,
            CallOptions? options)
            => Call<T>(token, args, self, options, convertResult: true, registerPackageRequest: null);

        private Output<T> Call<T>(
            string token,
            CallArgs args,
            Resource? self,
            CallOptions? options,
            bool convertResult,
            RegisterPackageRequest? registerPackageRequest = null)
            => new Output<T>(CallAsync<T>(token, args, self, options, convertResult, registerPackageRequest));

        private async Task<OutputData<T>> CallAsync<T>(
            string token, CallArgs args, Resource? self, CallOptions? options, bool convertResult, RegisterPackageRequest? registerPackageRequest = null)
        {
            var (result, deps) = await CallRawAsync(token, args, self, options, registerPackageRequest).ConfigureAwait(false);
            if (convertResult)
            {
                var converted = Pulumi.Serialization.Converter.ConvertValue<T>(err => Log.Warn(err, self), $"{token} result", new Value { StructValue = result });
                return new OutputData<T>(deps, converted.Value, converted.IsKnown, converted.IsSecret);
            }

            return new OutputData<T>(ImmutableHashSet<Resource>.Empty, default!, isKnown: true, isSecret: false);
        }

        private async Task<(Struct Return, ImmutableHashSet<Resource> Dependencies)> CallRawAsync(
            string token, CallArgs args, Resource? self, CallOptions? options, RegisterPackageRequest? registerPackageRequest = null)
        {
            var label = $"Calling function: token={token} asynchronously";
            Log.Debug(label);

            // Be resilient to misbehaving callers.
            // ReSharper disable once ConstantNullCoalescingCondition
            args ??= CallArgs.Empty;

            // Wait for all values to be available, and then perform the RPC.
            var argsDict = await args.ToDictionaryAsync().ConfigureAwait(false);

            // If we have a self arg, include it in the args.
            if (self != null)
            {
                argsDict = argsDict.SetItem(SelfArg, self);
            }

            var (serialized, argDependencies) = await SerializeFilteredPropertiesAsync(
                    $"call:{token}",
                    argsDict, _ => true,
                    keepResources: true,
                    keepOutputValues: await MonitorSupportsOutputValues().ConfigureAwait(false),
                    excludeResourceReferencesFromDependencies: true).ConfigureAwait(false);
            Log.Debug($"Call RPC prepared: token={token}" +
                (_excessiveDebugOutput ? $", obj={serialized}" : ""));

            // Determine the provider and version to use.
            ProviderResource? provider;
            string? version;
            string? pluginDownloadURL;
            if (self != null)
            {
                provider = self._provider;
                version = self._version;
                pluginDownloadURL = self._pluginDownloadURL;
            }
            else
            {
                provider = GetProvider(token, options);
                version = options?.Version;
                pluginDownloadURL = options?.PluginDownloadURL;
            }
            var providerReference = await ProviderResource.RegisterAsync(provider).ConfigureAwait(false);

            // Create the request.
            var request = new ResourceCallRequest
            {
                Tok = token,
                Provider = providerReference ?? "",
                Version = version ?? "",
                PluginDownloadURL = pluginDownloadURL ?? "",
                Args = serialized,
            };

            if (registerPackageRequest != null)
            {
                var packageRef = await ResolvePackageRef(registerPackageRequest).ConfigureAwait(false);
                if (packageRef != null)
                {
                    request.PackageRef = packageRef;
                }
            }

            // Add arg dependencies to the request.
            foreach (var (argName, directDependencies) in argDependencies)
            {
                var urns = await GetAllTransitivelyReferencedResourceUrnsAsync(directDependencies).ConfigureAwait(false);
                var deps = new ResourceCallRequest.Types.ArgumentDependencies();
                deps.Urns.AddRange(urns);
                request.ArgDependencies.Add(argName, deps);
            }

            // Kick off the call.
            var result = await Monitor.CallAsync(request).ConfigureAwait(false);

            // Handle failures.
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

                throw new CallException($"Call of '{token}' failed: {reasons}");
            }

            // Unmarshal return dependencies.
            var dependencies = ImmutableHashSet.CreateBuilder<Resource>();
            foreach (var (_, returnDependencies) in result.ReturnDependencies)
            {
                foreach (var urn in returnDependencies.Urns)
                {
                    dependencies.Add(new DependencyResource(urn));
                }
            }

            return (result.Return, dependencies.ToImmutable());
        }

        private static ProviderResource? GetProvider(string token, CallOptions? options)
            => options?.Provider ?? options?.Parent?.GetProvider(token);

        private sealed class CallException : Exception
        {
            public CallException(string error)
                : base(error)
            {
            }
        }
    }
}

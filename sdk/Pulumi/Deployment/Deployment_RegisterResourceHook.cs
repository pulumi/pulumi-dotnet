// Copyright 2025, Pulumi Corporation

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;

namespace Pulumi
{
    public partial class Deployment
    {
        Task IDeploymentInternal.RegisterResourceHook(ResourceHook hook)
        {
            Log.Debug($"RegisterResourceHook: registering task for {hook.Name}");
            var task = RegisterResourceHookAsync(hook);
            _runner.RegisterTask($"RegisterResourceHook: {hook.Name}", task);
            return task;
        }

        private async Task RegisterResourceHookAsync(ResourceHook hook)
        {
            Log.Debug($"RegisterResourceHook: registering {hook.Name} (OnDryRun={hook.Options.OnDryRun})");

            var monitorSupportsResourceHooks = await this.MonitorSupportsResourceHooks().ConfigureAwait(false);
            if (!monitorSupportsResourceHooks)
            {
                throw new InvalidOperationException("The Pulumi CLI does not support resource hooks. Please update the Pulumi CLI.");
            }

            var callbacks = await this.GetCallbacksAsync(CancellationToken.None).ConfigureAwait(false);
            var callback = await AllocateResourceHook(callbacks.Callbacks, hook).ConfigureAwait(false);

            var request = new Pulumirpc.RegisterResourceHookRequest()
            {
                Name = hook.Name,
                Callback = callback,
                OnDryRun = hook.Options.OnDryRun ?? false,
            };

            await Monitor.RegisterResourceHookAsync(request).ConfigureAwait(false);
            Log.Debug($"RegisterResourceHook resource hook: {hook.Name} (OnDryRun={hook.Options.OnDryRun})");
        }

        private static async Task<Pulumirpc.Callback> AllocateResourceHook(Callbacks callbacks, ResourceHook hook)
        {
            var wrapper = new Callback(async (message, token) =>
            {
                var request = Pulumirpc.ResourceHookRequest.Parser.ParseFrom(message);

                ImmutableDictionary<string, object?>? newInputs = null;
                if (request.NewInputs != null)
                {
                    newInputs = DeserializeStruct(request.NewInputs);
                }

                ImmutableDictionary<string, object?>? oldInputs = null;
                if (request.OldInputs != null)
                {
                    oldInputs = DeserializeStruct(request.OldInputs);
                }

                ImmutableDictionary<string, object?>? newOutputs = null;
                if (request.NewOutputs != null)
                {
                    newOutputs = DeserializeStruct(request.NewOutputs);
                }

                ImmutableDictionary<string, object?>? oldOutputs = null;
                if (request.OldOutputs != null)
                {
                    oldOutputs = DeserializeStruct(request.OldOutputs);
                }

                var args = new ResourceHookArgs(request.Urn, request.Id, newInputs, oldInputs, newOutputs, oldOutputs);

                await hook.Callback(args, token);

                var response = new Pulumirpc.ResourceHookResponse();

                return response;
            });

            return await callbacks.AllocateCallback(wrapper);
        }

        private static ImmutableDictionary<string, object?> DeserializeStruct(Struct s)
        {
            var builder = ImmutableDictionary.CreateBuilder<string, object?>();
            foreach (var kv in s.Fields)
            {
                var outputData = Serialization.Deserializer.Deserialize(kv.Value);
                if (outputData.IsSecret)
                {
                    builder.Add(kv.Key, Output.CreateSecret(outputData.Value));
                }
                else
                {
                    builder.Add(kv.Key, outputData.Value);
                }
            }

            return builder.ToImmutable();
        }
    }
}

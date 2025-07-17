// Copyright 2025, Pulumi Corporation

using System.Linq;
using System.Threading.Tasks;

namespace Pulumi
{
    /// <summary>
    /// Utilities for working with resource hooks, particularly for converting
    /// between protobuf representations and .NET ResourceHook objects.
    /// </summary>
    internal static class ResourceHookUtilities
    {
        /// <summary>
        /// Converts a protobuf ResourceHooksBinding from
        /// RegisterResourceRequest to a .NET ResourceHookBinding using
        /// StubResourceHook instances. This is used when receiving hooks from
        /// transforms that need to be reconstructed as .NET objects.
        /// </summary>
        internal static ResourceHookBinding? ResourceHookBindingFromProto(Pulumirpc.RegisterResourceRequest.Types.ResourceHooksBinding? protoBinding)
        {
            if (protoBinding == null)
            {
                return null;
            }

            var hooks = new ResourceHookBinding();
            hooks.BeforeCreate.AddRange(protoBinding.BeforeCreate.Select(name => new StubResourceHook(name)));
            hooks.AfterCreate.AddRange(protoBinding.AfterCreate.Select(name => new StubResourceHook(name)));
            hooks.BeforeUpdate.AddRange(protoBinding.BeforeUpdate.Select(name => new StubResourceHook(name)));
            hooks.AfterUpdate.AddRange(protoBinding.AfterUpdate.Select(name => new StubResourceHook(name)));
            hooks.BeforeDelete.AddRange(protoBinding.BeforeDelete.Select(name => new StubResourceHook(name)));
            hooks.AfterDelete.AddRange(protoBinding.AfterDelete.Select(name => new StubResourceHook(name)));
            return hooks;
        }

        /// <summary>
        /// Converts a protobuf ResourceHooksBinding from ConstructRequest to a
        /// .NET ResourceHookBinding using StubResourceHook instances. This is
        /// used when receiving hooks from remote components that need to be
        /// reconstructed as .NET objects.
        /// </summary>
        internal static ResourceHookBinding? ResourceHookBindingFromProto(Pulumirpc.ConstructRequest.Types.ResourceHooksBinding? protoBinding)
        {
            if (protoBinding == null)
            {
                return null;
            }

            var hooks = new ResourceHookBinding();
            hooks.BeforeCreate.AddRange(protoBinding.BeforeCreate.Select(name => new StubResourceHook(name)));
            hooks.AfterCreate.AddRange(protoBinding.AfterCreate.Select(name => new StubResourceHook(name)));
            hooks.BeforeUpdate.AddRange(protoBinding.BeforeUpdate.Select(name => new StubResourceHook(name)));
            hooks.AfterUpdate.AddRange(protoBinding.AfterUpdate.Select(name => new StubResourceHook(name)));
            hooks.BeforeDelete.AddRange(protoBinding.BeforeDelete.Select(name => new StubResourceHook(name)));
            hooks.AfterDelete.AddRange(protoBinding.AfterDelete.Select(name => new StubResourceHook(name)));
            return hooks;
        }

        /// <summary>
        /// StubResourceHook is a resource hook that does nothing.
        ///
        /// We need to reconstruct ResourceHook instances when receiving hooks
        /// from transforms or remote components, but we only have the name
        /// available. We know these hooks have already been registered, so we
        /// can construct dummy hooks here that will be serialized back into a
        /// list of hook names.
        /// </summary>
        internal class StubResourceHook : ResourceHook
        {
            private static readonly ResourceHookCallback _doNothing = (args, cancellationToken) => Task.CompletedTask;
            private static readonly ResourceHookOptions _noOptions = new();

            public StubResourceHook(string name)
                : base(name, _doNothing, _noOptions, Task.CompletedTask)
            {
            }
        }
    }
}

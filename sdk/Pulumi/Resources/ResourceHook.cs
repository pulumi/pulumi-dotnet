// Copyright 2025, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Pulumi
{
    /// <summary>
    /// A resource hook is a callback that can be registered to run at specific points in the resource lifecycle.
    /// </summary>
    public class ResourceHook
    {
        /// <summary>
        /// The name of the resource hook. This must be unique within a program.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The callback to invoke when the resource hook is triggered.
        /// </summary>
        public ResourceHookCallback Callback { get; }

        /// <summary>
        /// Options for the resource hook, such as whether it should run during a dry-run operation
        /// (e.g. <c>preview</c>).
        /// </summary>
        public ResourceHookOptions Options { get; }

        internal readonly Task _registered;

        /// <summary>
        /// Creates a new <see cref="ResourceHook"/> with the specified name, callback, and options.
        /// </summary>
        public ResourceHook(string name, ResourceHookCallback callback, ResourceHookOptions? options = null)
        {
            Name = name;
            Callback = callback;
            Options = options ?? new ResourceHookOptions();

            // Register the resource hook with the active deployment. Resources depending on this hook will await this
            // task to ensure the hook is registered before they register themselves.
            _registered = Deployment.InternalInstance.RegisterResourceHook(this);
        }

        /// <summary>
        /// Creates a new <see cref="ResourceHook"/> with the specified name, callback, and options. The task
        /// determining whether or not the hook has been registered must also be provided. Typically this will be a
        /// registration task attached to the current deployment, but subclasses may use this constructor to
        /// initialize hooks with custom registration logic.
        /// </summary>
        protected ResourceHook(string name, ResourceHookCallback callback, ResourceHookOptions options, Task registered)
        {
            Name = name;
            Callback = callback;
            Options = options ?? new ResourceHookOptions();
            _registered = registered;
        }
    }

    /// <summary>
    /// ResourceHookCallback is a delegate that defines the signature of <see cref="ResourceHook"/> callback functions.
    /// Callbacks take a set of <see cref="ResourceHookArgs"/> and an optional <see cref="CancellationToken"/>.
    /// Callbacks may raise an error by throwing an exception, which will be propagated to the resource operation that
    /// triggered the hook.
    /// </summary>
    public delegate Task ResourceHookCallback(ResourceHookArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// <para>
    /// ErrorHookArgs represents the arguments passed to an error hook. Depending on the failed operation, only some of
    /// the new/old inputs/outputs are set.
    /// </para>
    /// <code>
    /// | Failed Operation | old_inputs | new_inputs | old_outputs |
    /// | ---------------- | ---------- | ---------- | ----------- |
    /// | create           |            | ✓          |             |
    /// | update           | ✓          | ✓          | ✓           |
    /// | delete           | ✓          |            | ✓           |
    /// </code>
    /// </summary>
    public class ErrorHookArgs
    {
        /// <summary>The URN of the resource that triggered the hook.</summary>
        public string Urn { get; }
        /// <summary>The ID of the resource that triggered the hook.</summary>
        public string Id { get; }
        /// <summary>The name of the resource that triggered the hook.</summary>
        public string Name { get; }
        /// <summary>The type of the resource that triggered the hook.</summary>
        public string Type { get; }
        /// <summary>The new inputs of the resource that triggered the hook.</summary>
        public ImmutableDictionary<string, object?>? NewInputs { get; }
        /// <summary>The old inputs of the resource that triggered the hook.</summary>
        public ImmutableDictionary<string, object?>? OldInputs { get; }
        /// <summary>The old outputs of the resource that triggered the hook.</summary>
        public ImmutableDictionary<string, object?>? OldOutputs { get; }
        /// <summary>The operation that failed (create, update, or delete).</summary>
        public string FailedOperation { get; }
        /// <summary>The errors that have been seen so far (newest first).</summary>
        public IReadOnlyList<string> Errors { get; }

        public ErrorHookArgs(
            string urn,
            string id,
            string name,
            string type,
            ImmutableDictionary<string, object?>? newInputs = null,
            ImmutableDictionary<string, object?>? oldInputs = null,
            ImmutableDictionary<string, object?>? oldOutputs = null,
            string failedOperation = "",
            IReadOnlyList<string>? errors = null)
        {
            Urn = urn;
            Id = id;
            Name = name;
            Type = type;
            NewInputs = newInputs;
            OldInputs = oldInputs;
            OldOutputs = oldOutputs;
            FailedOperation = failedOperation;
            Errors = errors ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// ErrorHookCallback is a delegate that defines the signature of <see cref="ErrorHook"/> callback functions.
    /// Returns true to retry the operation, false to not retry.
    /// </summary>
    public delegate Task<bool> ErrorHookCallback(ErrorHookArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// An error hook is a callback that can be registered to run when a resource operation fails. The callback can
    /// return true to retry the operation or false to not retry.
    /// </summary>
    public class ErrorHook
    {
        /// <summary>The name of the error hook. This must be unique within a program.</summary>
        public string Name { get; }
        /// <summary>The callback to invoke when the error hook is triggered.</summary>
        public ErrorHookCallback Callback { get; }

        internal readonly Task _registered;

        /// <summary>
        /// Creates a new <see cref="ErrorHook"/> with the specified name and callback.
        /// </summary>
        public ErrorHook(string name, ErrorHookCallback callback)
        {
            Name = name;
            Callback = callback;
            _registered = Deployment.InternalInstance.RegisterErrorHook(this);
        }

        /// <summary>
        /// Creates a new <see cref="ErrorHook"/> with the specified name, callback, and registration task. Used by
        /// <see cref="ResourceHookUtilities.StubErrorHook"/> when reconstructing hooks from proto.
        /// </summary>
        protected internal ErrorHook(string name, ErrorHookCallback callback, Task registered)
        {
            Name = name;
            Callback = callback;
            _registered = registered;
        }
    }

    /// <summary>
    /// Options for registering a <see cref="ResourceHook"/>.
    /// </summary>
    public class ResourceHookOptions
    {
        /// <summary>
        /// Run the hook during dry-run (<c>preview</c>) operations. Defaults to false.
        /// </summary>
        public bool? OnDryRun { get; set; }
    }

    /// <summary>
    /// <para>
    /// ResourceHookArgs represents the arguments passed to a resource hook. Depending on the hook type, only some of
    /// the new/old inputs/outputs are set.
    /// </para>
    /// <code>
    /// | Hook Type     | old_inputs | new_inputs | old_outputs | new_outputs |
    /// | ------------- | ---------- | ---------- | ----------- | ----------- |
    /// | before_create |            | ✓          |             |             |
    /// | after_create  |            | ✓          |             | ✓           |
    /// | before_update | ✓          | ✓          | ✓           |             |
    /// | after_update  | ✓          | ✓          | ✓           | ✓           |
    /// | before_delete | ✓          |            | ✓           |             |
    /// | after_delete  | ✓          |            | ✓           |             |
    /// </code>
    /// </summary>
    public class ResourceHookArgs
    {
        /// <summary>
        /// The URN of the resource that triggered the hook.
        /// </summary>
        public string Urn { get; }

        /// <summary>
        /// The ID of the resource that triggered the hook.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// The logical name of the resource that triggered the hook.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The resource type token of the resource that triggered the hook.
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// The new inputs of the resource that triggered the hook.
        /// </summary>
        public ImmutableDictionary<string, object?>? NewInputs { get; }

        /// <summary>
        /// The old inputs of the resource that triggered the hook.
        /// </summary>
        public ImmutableDictionary<string, object?>? OldInputs { get; }

        /// <summary>
        /// The new outputs of the resource that triggered the hook.
        /// </summary>
        public ImmutableDictionary<string, object?>? NewOutputs { get; }

        /// <summary>
        /// The old outputs of the resource that triggered the hook.
        /// </summary>
        public ImmutableDictionary<string, object?>? OldOutputs { get; }

        /// <summary>
        /// Creates a new set of <see cref="ResourceHookArgs"/>.
        /// </summary>
        public ResourceHookArgs(
          string urn,
          string id,
          string name,
          string type,
          ImmutableDictionary<string, object?>? newInputs = null,
          ImmutableDictionary<string, object?>? oldInputs = null,
          ImmutableDictionary<string, object?>? newOutputs = null,
          ImmutableDictionary<string, object?>? oldOutputs = null)
        {
            Urn = urn;
            Id = id;
            Name = name;
            Type = type;
            NewInputs = newInputs;
            OldInputs = oldInputs;
            NewOutputs = newOutputs;
            OldOutputs = oldOutputs;
        }
    }

    /// <summary>
    /// <para>
    /// Binds <see cref="ResourceHook"/>s to a resource. The resource hooks will be invoked at specific points in the
    /// lifecycle of the resource.
    /// </para>
    /// <para>
    /// <c>Before*</c> hooks that raise an exception will cause the resource operation to fail. <c>After*</c> hooks that
    /// raise an exception will log a warning, but do not cause the action or the deployment to fail.
    /// </para>
    /// <para>
    /// When running <c>pulumi destroy</c>, <c>BeforeDelete</c> and <c>AfterDelete</c> resource hooks require the
    /// operation to run with <c>--run-program</c>, to ensure that the program which defines the hooks is available.
    /// </para>
    /// </summary>
    public class ResourceHookBinding
    {
        private List<ResourceHook>? _beforeCreate;

        /// <summary>
        /// Hooks to be invoked before the resource is created.
        /// </summary>
        public List<ResourceHook> BeforeCreate
        {
            get => _beforeCreate ??= new List<ResourceHook>();
            set => _beforeCreate = value;
        }

        private List<ResourceHook>? _afterCreate;

        /// <summary>
        /// Hooks to be invoked after the resource is created.
        /// </summary>
        public List<ResourceHook> AfterCreate
        {
            get => _afterCreate ??= new List<ResourceHook>();
            set => _afterCreate = value;
        }

        private List<ResourceHook>? _beforeUpdate;

        /// <summary>
        /// Hooks to be invoked before the resource is updated.
        /// </summary>
        public List<ResourceHook> BeforeUpdate
        {
            get => _beforeUpdate ??= new List<ResourceHook>();
            set => _beforeUpdate = value;
        }

        private List<ResourceHook>? _afterUpdate;

        /// <summary>
        /// Hooks to be invoked after the resource is updated.
        /// </summary>
        public List<ResourceHook> AfterUpdate
        {
            get => _afterUpdate ??= new List<ResourceHook>();
            set => _afterUpdate = value;
        }

        private List<ResourceHook>? _beforeDelete;

        /// <summary>
        /// Hooks to be invoked before the resource is deleted.
        /// </summary>
        public List<ResourceHook> BeforeDelete
        {
            get => _beforeDelete ??= new List<ResourceHook>();
            set => _beforeDelete = value;
        }

        private List<ResourceHook>? _afterDelete;

        /// <summary>
        /// Hooks to be invoked after the resource is deleted.
        /// </summary>
        public List<ResourceHook> AfterDelete
        {
            get => _afterDelete ??= new List<ResourceHook>();
            set => _afterDelete = value;
        }

        private List<ErrorHook>? _onError;

        /// <summary>
        /// Hooks to be invoked when a resource operation fails. Return true to retry, false to not retry.
        /// </summary>
        public List<ErrorHook> OnError
        {
            get => _onError ??= new List<ErrorHook>();
            set => _onError = value;
        }

        /// <summary>
        /// IsEmpty is true if and only if no hooks have been bound to any of the lifecycle events.
        /// </summary>
        public bool IsEmpty =>
            BeforeCreate.Count == 0 &&
            AfterCreate.Count == 0 &&
            BeforeUpdate.Count == 0 &&
            AfterUpdate.Count == 0 &&
            BeforeDelete.Count == 0 &&
            AfterDelete.Count == 0 &&
            OnError.Count == 0;

        /// <summary>
        /// Creates a deep copy of this <see cref="ResourceHookBinding"/> instance.
        /// </summary>
        public ResourceHookBinding Clone()
        {
            return new ResourceHookBinding
            {
                BeforeCreate = BeforeCreate.ToList(),
                AfterCreate = AfterCreate.ToList(),
                BeforeUpdate = BeforeUpdate.ToList(),
                AfterUpdate = AfterUpdate.ToList(),
                BeforeDelete = BeforeDelete.ToList(),
                AfterDelete = AfterDelete.ToList(),
                OnError = OnError.ToList(),
            };
        }

        /// <summary>
        /// Concatenates this <see cref="ResourceHookBinding"/> with another , merging their hooks.
        /// </summary>
        public ResourceHookBinding Concat(ResourceHookBinding other)
        {
            return new ResourceHookBinding
            {
                BeforeCreate = BeforeCreate.Concat(other.BeforeCreate).ToList(),
                AfterCreate = AfterCreate.Concat(other.AfterCreate).ToList(),
                BeforeUpdate = BeforeUpdate.Concat(other.BeforeUpdate).ToList(),
                AfterUpdate = AfterUpdate.Concat(other.AfterUpdate).ToList(),
                BeforeDelete = BeforeDelete.Concat(other.BeforeDelete).ToList(),
                AfterDelete = AfterDelete.Concat(other.AfterDelete).ToList(),
                OnError = OnError.Concat(other.OnError).ToList(),
            };
        }
    }
}

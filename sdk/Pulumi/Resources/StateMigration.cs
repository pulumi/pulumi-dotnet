// Copyright 2026, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Pulumi
{
    /// <summary>
    /// <see cref="StateMigration"/> is the callback signature for the <see cref="ResourceOptions.StateMigrations"/>
    /// resource option.
    /// <para>
    /// This API is experimental and may change.
    /// </para>
    /// <para>
    /// A callback receives the prior state of the resource and its descendants, and may return a replacement subtree
    /// for the engine to use before diffing those resources. Returning <see langword="null"/> leaves the callback's
    /// input state unchanged and allows later callbacks to run. Callbacks must be idempotent.
    /// </para>
    /// <para>
    /// Migrations run during updates and previews when prior state exists, including state matched through aliases.
    /// Migrations rewrite state only, they do not create, import, or modify physical resources.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The callback receives plaintext secret values inside their secret envelopes and must not log or otherwise
    /// expose them. It must not perform Pulumi runtime operations or wait for unresolved Outputs. Every resource omitted
    /// from the returned state must identify a returned successor. Provider resource states must remain unchanged,
    /// and custom resources must preserve their physical identity and lifecycle safety flags.
    /// </remarks>
    public delegate Task<StateMigrationResult?> StateMigration(StateMigrationArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// <see cref="StateMigrationArgs"/> contains the prior state of the resource and its descendants in
    /// <see href="https://pulumi-developer-docs.readthedocs.io/latest/docs/references/deployment-schema.html#pulumi-resource-state">checkpoint resource format</see>.
    /// <para>
    /// This API is experimental and may change.
    /// </para>
    /// </summary>
    public sealed class StateMigrationArgs
    {
        /// <summary>
        /// The URN of the resource being registered. This may differ from its URN in <see cref="OldState"/>
        /// when the prior resource is matched through an alias.
        /// </summary>
        public string Urn { get; }

        /// <summary>
        /// The prior state of the resource and its descendants in
        /// <see href="https://pulumi-developer-docs.readthedocs.io/latest/docs/references/deployment-schema.html#pulumi-resource-state">checkpoint resource format</see>,
        /// with the resource itself first. For subsequent callbacks, this includes changes made by earlier callbacks
        /// in the chain.
        /// </summary>
        public JsonArray OldState { get; }

        public StateMigrationArgs(string urn, JsonArray oldState)
        {
            Urn = urn ?? throw new ArgumentNullException(nameof(urn));
            OldState = oldState ?? throw new ArgumentNullException(nameof(oldState));
        }
    }

    /// <summary>
    /// <see cref="StateMigrationResult"/> is returned by a state migration callback when it changes the state.
    /// Every resource present in the old state must either be returned in <see cref="NewState"/> under the same URN
    /// or have an entry in <see cref="Successors"/>, but not both.
    /// <para>
    /// This API is experimental and may change.
    /// </para>
    /// </summary>
    public sealed class StateMigrationResult
    {
        /// <summary>
        /// The complete migrated subtree in
        /// <see href="https://pulumi-developer-docs.readthedocs.io/latest/docs/references/deployment-schema.html#pulumi-resource-state">checkpoint resource format</see>,
        /// including unchanged resources. This replaces <see cref="StateMigrationArgs.OldState"/>.
        /// </summary>
        public JsonArray NewState { get; }

        /// <summary>
        /// Maps each old URN removed from the state to the URN in <see cref="NewState"/> that succeeds it.
        /// Multiple old URNs may map to the same successor. A resource cannot be removed without a successor.
        /// The engine uses these mappings to rewrite resource references.
        /// </summary>
        public IReadOnlyDictionary<string, string> Successors { get; }

        public StateMigrationResult(JsonArray newState, IReadOnlyDictionary<string, string>? successors = null)
        {
            NewState = newState ?? throw new ArgumentNullException(nameof(newState));
            Successors = successors ?? ImmutableDictionary<string, string>.Empty;
        }
    }
}

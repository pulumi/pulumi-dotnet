// Copyright 2016-2026, Pulumi Corporation

// The hand-written half of the `API` partial class: it executes the argument
// vector built by the generated command methods in Commands.cs through the
// SDK's existing PulumiCommand infrastructure.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Pulumi.Automation.Commands
{
    public sealed partial class API
    {
        private readonly PulumiCommand _command;

        /// <summary>
        /// Creates an API backed by the given PulumiCommand.
        /// </summary>
        public API(PulumiCommand command)
        {
            this._command = command ?? throw new ArgumentNullException(nameof(command));
        }

        private Task<CommandResult> RunAsync(
            IReadOnlyList<string> arguments,
            BaseOptions? options,
            CancellationToken cancellationToken)
            => this._command.RunAsync(
                arguments.ToList(),
                options?.WorkDir ?? Environment.CurrentDirectory,
                options?.EnvironmentVariables ?? new Dictionary<string, string?>(),
                options?.OnStandardOutput,
                options?.OnStandardError,
                onEngineEvent: null,
                cancellationToken);
    }
}

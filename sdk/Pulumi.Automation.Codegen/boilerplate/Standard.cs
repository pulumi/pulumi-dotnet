// Copyright 2016-2026, Pulumi Corporation

// The production half of the generated `API` partial class: it executes the
// argument vector built by the generated command methods through the SDK's
// existing PulumiCommand infrastructure. The integration PR compiles this
// alongside the generated Options.cs and Commands.cs inside Pulumi.Automation.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pulumi.Automation.Commands;

namespace Pulumi.Automation.Interface
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

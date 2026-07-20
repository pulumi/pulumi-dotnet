// Copyright 2016-2026, Pulumi Corporation

// The testing half of the generated `API` partial class: instead of running
// the CLI it records the argument vector and options the generated command
// methods produce, so the code generator's tests can assert on them without a
// Pulumi CLI. Swapped in for Standard.cs when compiling for tests.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pulumi.Automation.Commands;

namespace Pulumi.Automation.Interface
{
    public sealed partial class API
    {
        /// <summary>
        /// The argument vector produced by the most recent command call.
        /// </summary>
        public IReadOnlyList<string>? LastArguments { get; private set; }

        /// <summary>
        /// The options passed to the most recent command call.
        /// </summary>
        public BaseOptions? LastOptions { get; private set; }

        private Task<CommandResult> RunAsync(
            IReadOnlyList<string> arguments,
            BaseOptions? options,
            CancellationToken cancellationToken)
        {
            this.LastArguments = arguments;
            this.LastOptions = options;
            return Task.FromResult(new CommandResult(0, string.Empty, string.Empty));
        }
    }
}

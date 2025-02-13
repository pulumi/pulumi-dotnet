using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pulumi.Automation.Events;
using Pulumi.Automation.Commands;
using Semver;

namespace Pulumi.Automation.Tests.Mocks
{
    class PulumiCommandMock : PulumiCommand
    {
        private readonly CommandResult CommandResult;

        public PulumiCommandMock(SemVersion version, CommandResult commandResult)
        {
            this.Version = version;
            this.CommandResult = commandResult;
        }

        public override Task<CommandResult> RunAsync(
            IList<string> args,
            string workingDir,
            IDictionary<string, string?> additionalEnv,
            Action<string>? onStandardOutput = null,
            Action<string>? onStandardError = null,
            Action<EngineEvent>? onEngineEvent = null,
            CancellationToken cancellationToken = default)
        {
            this.RecordedArgs = args;
            return Task.FromResult(this.CommandResult);
        }

        public override Task<CommandResult> RunInputAsync(
            IList<string> args,
            string workingDir,
            IDictionary<string, string?> additionalEnv,
            Action<string>? onStandardOutput = null,
            Action<string>? onStandardError = null,
            string? stdIn = null,
            Action<EngineEvent>? onEngineEvent = null,
            CancellationToken cancellationToken = default)
        {
            this.RecordedArgs = args;
            return Task.FromResult(this.CommandResult);
        }

        public override SemVersion? Version { get; }

        public IList<string> RecordedArgs { get; private set; } = new List<string>();
    }
}

// Copyright 2016-2022, Pulumi Corporation

using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Pulumi.Automation
{
    /// <summary>
    /// Extensibility options to configure a LocalWorkspace; e.g: settings to seed
    /// and environment variables to pass through to every command.
    /// </summary>
    public class LocalWorkspaceOptions
    {
        /// <summary>
        /// The directory to run Pulumi commands and read settings (Pulumi.yaml and Pulumi.{stack}.yaml).
        /// </summary>
        public string? WorkDir { get; set; }

        /// <summary>
        /// The directory to override for CLI metadata.
        /// </summary>
        public string? PulumiHome { get; set; }


        /// <summary>
        /// The Pulumi CLI installation to use.
        /// </summary>
        public Commands.PulumiCommand? PulumiCommand { get; set; }

        /// <summary>
        /// The secrets provider to user for encryption and decryption of stack secrets.
        /// <para/>
        /// See: https://www.pulumi.com/docs/intro/concepts/secrets/#available-encryption-providers
        /// </summary>
        public string? SecretsProvider { get; set; }

        /// <summary>
        /// The inline program <see cref="PulumiFn"/> to be used for Preview/Update operations if any.
        /// <para/>
        /// If none is specified, the stack will refer to <see cref="Automation.ProjectSettings"/> for this information.
        /// </summary>
        public PulumiFn? Program { get; set; }

        /// <summary>
        /// A custom logger instance that will be used for inline programs. Note that it will only be used
        /// if <see cref="Program"/> is also provided.
        /// </summary>
        public ILogger? Logger { get; set; }

        /// <summary>
        /// Environment values scoped to the current workspace. These will be supplied to every
        /// Pulumi command.
        /// </summary>
        public IDictionary<string, string?>? EnvironmentVariables { get; set; }

        /// <summary>
        /// The settings object for the current project.
        /// <para/>
        /// If provided when initializing <see cref="LocalWorkspace"/> a project settings
        /// file will be written to when the workspace is initialized via
        /// <see cref="LocalWorkspace.SaveProjectSettingsAsync(Automation.ProjectSettings, System.Threading.CancellationToken)"/>.
        /// </summary>
        public ProjectSettings? ProjectSettings { get; set; }

        /// <summary>
        /// A map of Stack names and corresponding settings objects.
        /// <para/>
        /// If provided when initializing <see cref="LocalWorkspace"/> stack settings
        /// file(s) will be written to when the workspace is initialized via
        /// <see cref="LocalWorkspace.SaveStackSettingsAsync(string, Automation.StackSettings, System.Threading.CancellationToken)"/>.
        /// </summary>
        public IDictionary<string, StackSettings>? StackSettings { get; set; }

        /// <summary>
        /// Whether the workspace is a remote workspace.
        /// </summary>
        internal bool Remote { get; set; }

        /// <summary>
        /// Args for remote workspace with Git source.
        /// </summary>
        internal RemoteGitProgramArgs? RemoteGitProgramArgs { get; set; }

        /// <summary>
        /// Environment values scoped to the remote workspace. These will be passed to remote operations.
        /// </summary>
        internal IDictionary<string, EnvironmentVariableValue>? RemoteEnvironmentVariables { get; set; }

        /// <summary>
        /// An optional list of arbitrary commands to run before a remote Pulumi operation is invoked.
        /// </summary>
        internal IList<string>? RemotePreRunCommands { get; set; }

        /// <summary>
        /// Optional information about a remote execution image.
        /// </summary>
        internal ExecutorImage? RemoteExecutorImage { get; set; }

        /// <summary>
        /// Whether to skip the default dependency installation step.
        /// </summary>
        internal bool RemoteSkipInstallDependencies { get; set; }
    }
}

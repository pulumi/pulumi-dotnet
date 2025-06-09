// Copyright 2016-2022, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulumi.Automation.Commands;
using Pulumi.Automation.Exceptions;
using Pulumi.Automation.Serialization;
using Semver;

namespace Pulumi.Automation
{
    /// <summary>
    /// LocalWorkspace is a default implementation of the Workspace interface.
    /// <para/>
    /// A Workspace is the execution context containing a single Pulumi project, a program,
    /// and multiple stacks.Workspaces are used to manage the execution environment,
    /// providing various utilities such as plugin installation, environment configuration
    /// ($PULUMI_HOME), and creation, deletion, and listing of Stacks.
    /// <para/>
    /// LocalWorkspace relies on Pulumi.yaml and Pulumi.{stack}.yaml as the intermediate format
    /// for Project and Stack settings.Modifying ProjectSettings will
    /// alter the Workspace Pulumi.yaml file, and setting config on a Stack will modify the Pulumi.{stack}.yaml file.
    /// This is identical to the behavior of Pulumi CLI driven workspaces.
    /// <para/>
    /// If not provided a working directory - causing LocalWorkspace to create a temp directory,
    /// than the temp directory will be cleaned up on <see cref="Dispose"/>.
    /// </summary>
    public sealed class LocalWorkspace : Workspace
    {
        private readonly LocalSerializer _serializer = new LocalSerializer();
        private readonly bool _ownsWorkingDir;
        private readonly RemoteGitProgramArgs? _remoteGitProgramArgs;
        private readonly IDictionary<string, EnvironmentVariableValue>? _remoteEnvironmentVariables;
        private readonly IList<string>? _remotePreRunCommands;
        private readonly bool _remoteSkipInstallDependencies;
        private readonly ExecutorImage? _remoteExecutorImage;

        internal Task ReadyTask { get; }

        /// <inheritdoc/>
        public override string WorkDir { get; }

        /// <inheritdoc/>
        public override string? PulumiHome { get; }

        /// <inheritdoc/>
        public override string PulumiVersion => _cmd.Version?.ToString() ?? throw new InvalidOperationException("Failed to get Pulumi version.");

        /// <inheritdoc/>
        public override string? SecretsProvider { get; }

        /// <inheritdoc/>
        public override PulumiFn? Program { get; set; }

        /// <inheritdoc/>
        public override ILogger? Logger { get; set; }

        /// <inheritdoc/>
        public override IDictionary<string, string?>? EnvironmentVariables { get; set; }

        /// <summary>
        /// Whether this workspace is a remote workspace.
        /// </summary>
        internal bool Remote { get; }

        /// <summary>
        /// Creates a workspace using the specified options. Used for maximal control and
        /// customization of the underlying environment before any stacks are created or selected.
        /// </summary>
        /// <param name="options">Options used to configure the workspace.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public static async Task<LocalWorkspace> CreateAsync(
            LocalWorkspaceOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var cmd = options?.PulumiCommand ?? await LocalPulumiCommand.CreateAsync(new LocalPulumiCommandOptions
            {
                SkipVersionCheck = OptOutOfVersionCheck(options?.EnvironmentVariables)
            }, cancellationToken);
            var ws = new LocalWorkspace(
                cmd,
                options,
                cancellationToken);
            await ws.ReadyTask.ConfigureAwait(false);
            return ws;
        }

        /// <summary>
        /// Creates a Stack with a <see cref="LocalWorkspace"/> utilizing the specified
        /// inline (in process) <see cref="LocalWorkspaceOptions.Program"/>. This program
        /// is fully debuggable and runs in process. If no <see cref="LocalWorkspaceOptions.ProjectSettings"/>
        /// option is specified, default project settings will be created on behalf of the user. Similarly, unless a
        /// <see cref="LocalWorkspaceOptions.WorkDir"/> option is specified, the working directory will default
        /// to a new temporary directory provided by the OS.
        /// </summary>
        /// <param name="args">
        ///     A set of arguments to initialize a Stack with an inline <see cref="PulumiFn"/> program
        ///     that runs in process, as well as any additional customizations to be applied to the
        ///     workspace.
        /// </param>
        public static Task<WorkspaceStack> CreateStackAsync(InlineProgramArgs args)
            => CreateStackAsync(args, default);

        /// <summary>
        /// Creates a Stack with a <see cref="LocalWorkspace"/> utilizing the specified
        /// inline (in process) <see cref="LocalWorkspaceOptions.Program"/>. This program
        /// is fully debuggable and runs in process. If no <see cref="LocalWorkspaceOptions.ProjectSettings"/>
        /// option is specified, default project settings will be created on behalf of the user. Similarly, unless a
        /// <see cref="LocalWorkspaceOptions.WorkDir"/> option is specified, the working directory will default
        /// to a new temporary directory provided by the OS.
        /// </summary>
        /// <param name="args">
        ///     A set of arguments to initialize a Stack with an inline <see cref="PulumiFn"/> program
        ///     that runs in process, as well as any additional customizations to be applied to the
        ///     workspace.
        /// </param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public static Task<WorkspaceStack> CreateStackAsync(InlineProgramArgs args, CancellationToken cancellationToken)
            => CreateStackHelperAsync(args, WorkspaceStack.CreateAsync, cancellationToken);

        /// <summary>
        /// Creates a Stack with a <see cref="LocalWorkspace"/> utilizing the local Pulumi CLI program
        /// from the specified <see cref="LocalWorkspaceOptions.WorkDir"/>. This is a way to create drivers
        /// on top of pre-existing Pulumi programs. This Workspace will pick up any available Settings
        /// files(Pulumi.yaml, Pulumi.{stack}.yaml).
        /// </summary>
        /// <param name="args">
        ///     A set of arguments to initialize a Stack with a pre-configured Pulumi CLI program that
        ///     already exists on disk, as well as any additional customizations to be applied to the
        ///     workspace.
        /// </param>
        public static Task<WorkspaceStack> CreateStackAsync(LocalProgramArgs args)
            => CreateStackAsync(args, default);

        /// <summary>
        /// Creates a Stack with a <see cref="LocalWorkspace"/> utilizing the local Pulumi CLI program
        /// from the specified <see cref="LocalWorkspaceOptions.WorkDir"/>. This is a way to create drivers
        /// on top of pre-existing Pulumi programs. This Workspace will pick up any available Settings
        /// files(Pulumi.yaml, Pulumi.{stack}.yaml).
        /// </summary>
        /// <param name="args">
        ///     A set of arguments to initialize a Stack with a pre-configured Pulumi CLI program that
        ///     already exists on disk, as well as any additional customizations to be applied to the
        ///     workspace.
        /// </param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public static Task<WorkspaceStack> CreateStackAsync(LocalProgramArgs args, CancellationToken cancellationToken)
            => CreateStackHelperAsync(args, WorkspaceStack.CreateAsync, cancellationToken);

        /// <summary>
        /// Selects an existing Stack with a <see cref="LocalWorkspace"/> utilizing the specified
        /// inline (in process) <see cref="LocalWorkspaceOptions.Program"/>. This program
        /// is fully debuggable and runs in process. If no <see cref="LocalWorkspaceOptions.ProjectSettings"/>
        /// option is specified, default project settings will be created on behalf of the user. Similarly, unless a
        /// <see cref="LocalWorkspaceOptions.WorkDir"/> option is specified, the working directory will default
        /// to a new temporary directory provided by the OS.
        /// </summary>
        /// <param name="args">
        ///     A set of arguments to initialize a Stack with an inline <see cref="PulumiFn"/> program
        ///     that runs in process, as well as any additional customizations to be applied to the
        ///     workspace.
        /// </param>
        public static Task<WorkspaceStack> SelectStackAsync(InlineProgramArgs args)
            => SelectStackAsync(args, default);

        /// <summary>
        /// Selects an existing Stack with a <see cref="LocalWorkspace"/> utilizing the specified
        /// inline (in process) <see cref="LocalWorkspaceOptions.Program"/>. This program
        /// is fully debuggable and runs in process. If no <see cref="LocalWorkspaceOptions.ProjectSettings"/>
        /// option is specified, default project settings will be created on behalf of the user. Similarly, unless a
        /// <see cref="LocalWorkspaceOptions.WorkDir"/> option is specified, the working directory will default
        /// to a new temporary directory provided by the OS.
        /// </summary>
        /// <param name="args">
        ///     A set of arguments to initialize a Stack with an inline <see cref="PulumiFn"/> program
        ///     that runs in process, as well as any additional customizations to be applied to the
        ///     workspace.
        /// </param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public static Task<WorkspaceStack> SelectStackAsync(InlineProgramArgs args, CancellationToken cancellationToken)
            => CreateStackHelperAsync(args, WorkspaceStack.SelectAsync, cancellationToken);

        /// <summary>
        /// Selects an existing Stack with a <see cref="LocalWorkspace"/> utilizing the local Pulumi CLI program
        /// from the specified <see cref="LocalWorkspaceOptions.WorkDir"/>. This is a way to create drivers
        /// on top of pre-existing Pulumi programs. This Workspace will pick up any available Settings
        /// files(Pulumi.yaml, Pulumi.{stack}.yaml).
        /// </summary>
        /// <param name="args">
        ///     A set of arguments to initialize a Stack with a pre-configured Pulumi CLI program that
        ///     already exists on disk, as well as any additional customizations to be applied to the
        ///     workspace.
        /// </param>
        public static Task<WorkspaceStack> SelectStackAsync(LocalProgramArgs args)
            => SelectStackAsync(args, default);

        /// <summary>
        /// Selects an existing Stack with a <see cref="LocalWorkspace"/> utilizing the local Pulumi CLI program
        /// from the specified <see cref="LocalWorkspaceOptions.WorkDir"/>. This is a way to create drivers
        /// on top of pre-existing Pulumi programs. This Workspace will pick up any available Settings
        /// files(Pulumi.yaml, Pulumi.{stack}.yaml).
        /// </summary>
        /// <param name="args">
        ///     A set of arguments to initialize a Stack with a pre-configured Pulumi CLI program that
        ///     already exists on disk, as well as any additional customizations to be applied to the
        ///     workspace.
        /// </param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public static Task<WorkspaceStack> SelectStackAsync(LocalProgramArgs args, CancellationToken cancellationToken)
            => CreateStackHelperAsync(args, WorkspaceStack.SelectAsync, cancellationToken);

        /// <summary>
        /// Creates or selects an existing Stack with a <see cref="LocalWorkspace"/> utilizing the specified
        /// inline (in process) <see cref="LocalWorkspaceOptions.Program"/>. This program
        /// is fully debuggable and runs in process. If no <see cref="LocalWorkspaceOptions.ProjectSettings"/>
        /// option is specified, default project settings will be created on behalf of the user. Similarly, unless a
        /// <see cref="LocalWorkspaceOptions.WorkDir"/> option is specified, the working directory will default
        /// to a new temporary directory provided by the OS.
        /// </summary>
        /// <param name="args">
        ///     A set of arguments to initialize a Stack with an inline <see cref="PulumiFn"/> program
        ///     that runs in process, as well as any additional customizations to be applied to the
        ///     workspace.
        /// </param>
        public static Task<WorkspaceStack> CreateOrSelectStackAsync(InlineProgramArgs args)
            => CreateOrSelectStackAsync(args, default);

        /// <summary>
        /// Creates or selects an existing Stack with a <see cref="LocalWorkspace"/> utilizing the specified
        /// inline (in process) <see cref="LocalWorkspaceOptions.Program"/>. This program
        /// is fully debuggable and runs in process. If no <see cref="LocalWorkspaceOptions.ProjectSettings"/>
        /// option is specified, default project settings will be created on behalf of the user. Similarly, unless a
        /// <see cref="LocalWorkspaceOptions.WorkDir"/> option is specified, the working directory will default
        /// to a new temporary directory provided by the OS.
        /// </summary>
        /// <param name="args">
        ///     A set of arguments to initialize a Stack with an inline <see cref="PulumiFn"/> program
        ///     that runs in process, as well as any additional customizations to be applied to the
        ///     workspace.
        /// </param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public static Task<WorkspaceStack> CreateOrSelectStackAsync(InlineProgramArgs args, CancellationToken cancellationToken)
            => CreateStackHelperAsync(args, WorkspaceStack.CreateOrSelectAsync, cancellationToken);

        /// <summary>
        /// Creates or selects an existing Stack with a <see cref="LocalWorkspace"/> utilizing the local Pulumi CLI program
        /// from the specified <see cref="LocalWorkspaceOptions.WorkDir"/>. This is a way to create drivers
        /// on top of pre-existing Pulumi programs. This Workspace will pick up any available Settings
        /// files(Pulumi.yaml, Pulumi.{stack}.yaml).
        /// </summary>
        /// <param name="args">
        ///     A set of arguments to initialize a Stack with a pre-configured Pulumi CLI program that
        ///     already exists on disk, as well as any additional customizations to be applied to the
        ///     workspace.
        /// </param>
        public static Task<WorkspaceStack> CreateOrSelectStackAsync(LocalProgramArgs args)
            => CreateOrSelectStackAsync(args, default);

        /// <summary>
        /// Creates or selects an existing Stack with a <see cref="LocalWorkspace"/> utilizing the local Pulumi CLI program
        /// from the specified <see cref="LocalWorkspaceOptions.WorkDir"/>. This is a way to create drivers
        /// on top of pre-existing Pulumi programs. This Workspace will pick up any available Settings
        /// files(Pulumi.yaml, Pulumi.{stack}.yaml).
        /// </summary>
        /// <param name="args">
        ///     A set of arguments to initialize a Stack with a pre-configured Pulumi CLI program that
        ///     already exists on disk, as well as any additional customizations to be applied to the
        ///     workspace.
        /// </param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public static Task<WorkspaceStack> CreateOrSelectStackAsync(LocalProgramArgs args, CancellationToken cancellationToken)
            => CreateStackHelperAsync(args, WorkspaceStack.CreateOrSelectAsync, cancellationToken);

        private static async Task<WorkspaceStack> CreateStackHelperAsync(
            InlineProgramArgs args,
            Func<string, Workspace, CancellationToken, Task<WorkspaceStack>> initFunc,
            CancellationToken cancellationToken)
        {
            if (args.ProjectSettings is null)
                throw new ArgumentNullException(nameof(args), $"{nameof(args.ProjectSettings)} cannot be null.");

            var ws = new LocalWorkspace(
                await LocalPulumiCommand.CreateAsync(new LocalPulumiCommandOptions
                {
                    SkipVersionCheck = OptOutOfVersionCheck(),
                }, cancellationToken),
                args,
                cancellationToken);
            await ws.ReadyTask.ConfigureAwait(false);

            return await initFunc(args.StackName, ws, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<WorkspaceStack> CreateStackHelperAsync(
            LocalProgramArgs args,
            Func<string, Workspace, CancellationToken, Task<WorkspaceStack>> initFunc,
            CancellationToken cancellationToken)
        {
            var ws = new LocalWorkspace(
                await LocalPulumiCommand.CreateAsync(new LocalPulumiCommandOptions
                {
                    SkipVersionCheck = OptOutOfVersionCheck()
                }, cancellationToken),
                args,
                cancellationToken);
            await ws.ReadyTask.ConfigureAwait(false);

            return await initFunc(args.StackName, ws, cancellationToken).ConfigureAwait(false);
        }

        internal LocalWorkspace(
            PulumiCommand cmd,
            LocalWorkspaceOptions? options,
            CancellationToken cancellationToken)
            : base(cmd)
        {
            string? dir = null;
            var readyTasks = new List<Task>();

            if (options != null)
            {
                if (!string.IsNullOrWhiteSpace(options.WorkDir))
                    dir = options.WorkDir;

                this.PulumiHome = options.PulumiHome;
                this.Program = options.Program;
                this.Logger = options.Logger;
                this.SecretsProvider = options.SecretsProvider;
                this.Remote = options.Remote;
                this._remoteGitProgramArgs = options.RemoteGitProgramArgs;
                this._remoteSkipInstallDependencies = options.RemoteSkipInstallDependencies;
                this._remoteExecutorImage = options.RemoteExecutorImage;

                if (options.EnvironmentVariables != null)
                    this.EnvironmentVariables = new Dictionary<string, string?>(options.EnvironmentVariables);

                if (options.RemoteEnvironmentVariables != null)
                    this._remoteEnvironmentVariables =
                        new Dictionary<string, EnvironmentVariableValue>(options.RemoteEnvironmentVariables);

                if (options.RemotePreRunCommands != null)
                {
                    this._remotePreRunCommands = new List<string>(options.RemotePreRunCommands);
                }
            }

            if (string.IsNullOrWhiteSpace(dir))
            {
                // note that csharp doesn't guarantee that Path.GetRandomFileName returns a name
                // for a file or folder that doesn't already exist.
                // we should be OK with the "automation-" prefix but a collision is still
                // theoretically possible
                dir = Path.Combine(Path.GetTempPath(), $"automation-{Path.GetRandomFileName()}");
                Directory.CreateDirectory(dir);
                this._ownsWorkingDir = true;
            }

            this.WorkDir = dir;

            readyTasks.Add(this.CheckRemoteSupport(cancellationToken));

            if (options?.ProjectSettings != null)
            {
                readyTasks.Add(this.InitializeProjectSettingsAsync(options.ProjectSettings, cancellationToken));
            }

            if (options?.StackSettings != null && options.StackSettings.Any())
            {
                foreach (var pair in options.StackSettings)
                    readyTasks.Add(this.SaveStackSettingsAsync(pair.Key, pair.Value, cancellationToken));
            }

            ReadyTask = Task.WhenAll(readyTasks);
        }

        private async Task InitializeProjectSettingsAsync(ProjectSettings projectSettings,
                                                          CancellationToken cancellationToken)
        {
            // If given project settings, we want to write them out to
            // the working dir. We do not want to override existing
            // settings with default settings though.

            var existingSettings = await this.GetProjectSettingsAsync(cancellationToken).ConfigureAwait(false);
            if (existingSettings == null)
            {
                await this.SaveProjectSettingsAsync(projectSettings, cancellationToken).ConfigureAwait(false);
            }
            else if (!projectSettings.IsDefault &&
                     !ProjectSettings.Comparer.Equals(projectSettings, existingSettings))
            {
                var path = this.FindSettingsFile();
                throw new ProjectSettingsConflictException(path);
            }
        }

        private static readonly string[] _settingsExtensions = { ".yaml", ".yml", ".json" };

        private static bool OptOutOfVersionCheck(IDictionary<string, string?>? EnvironmentVariables = null)
        {
            var hasSkipEnvVar = EnvironmentVariables?.ContainsKey(LocalPulumiCommand.SkipVersionCheckVar) ?? false;
            var optOut = hasSkipEnvVar || Environment.GetEnvironmentVariable(LocalPulumiCommand.SkipVersionCheckVar) != null;
            return optOut;
        }

        private async Task CheckRemoteSupport(CancellationToken cancellationToken)
        {
            // If remote was specified, ensure the CLI supports it.
            if (!OptOutOfVersionCheck(this.EnvironmentVariables) && Remote)
            {
                // See if `--remote` is present in `pulumi preview --help`'s output.
                var args = new[] { "preview", "--help" };
                var previewResult = await RunCommandAsync(args, cancellationToken).ConfigureAwait(false);
                if (!previewResult.StandardOutput.Contains("--remote"))
                {
                    throw new InvalidOperationException("The Pulumi CLI does not support remote operations. Please update the Pulumi CLI.");
                }
            }
        }

        /// <inheritdoc/>
        public override async Task<ProjectSettings?> GetProjectSettingsAsync(CancellationToken cancellationToken = default)
        {
            var path = this.FindSettingsFile();
            var isJson = Path.GetExtension(path) == ".json";
            if (!File.Exists(path))
            {
                return null;
            }
            var content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            if (isJson)
            {
                return this._serializer.DeserializeJson<ProjectSettings>(content);
            }
            var model = this._serializer.DeserializeYaml<ProjectSettingsModel>(content);
            return model.Convert();
        }

        /// <inheritdoc/>
        public override Task SaveProjectSettingsAsync(ProjectSettings settings, CancellationToken cancellationToken = default)
        {
            var path = this.FindSettingsFile();
            var ext = Path.GetExtension(path);
            var content = ext == ".json" ? this._serializer.SerializeJson(settings) : this._serializer.SerializeYaml(settings);
            return File.WriteAllTextAsync(path, content, cancellationToken);
        }

        private string FindSettingsFile()
        {
            foreach (var ext in _settingsExtensions)
            {
                var testPath = Path.Combine(this.WorkDir, $"Pulumi{ext}");
                if (File.Exists(testPath))
                {
                    return testPath;
                }
            }
            var defaultPath = Path.Combine(this.WorkDir, "Pulumi.yaml");
            return defaultPath;
        }

        private static string GetStackSettingsName(string stackName)
        {
            var parts = stackName.Split('/');
            if (parts.Length < 1)
                return stackName;

            return parts[^1];
        }

        /// <inheritdoc/>
        public override async Task<StackSettings?> GetStackSettingsAsync(string stackName, CancellationToken cancellationToken = default)
        {
            var settingsName = GetStackSettingsName(stackName);

            foreach (var ext in _settingsExtensions)
            {
                var isJson = ext == ".json";
                var path = Path.Combine(this.WorkDir, $"Pulumi.{settingsName}{ext}");
                if (!File.Exists(path))
                    continue;

                var content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                return isJson ? this._serializer.DeserializeJson<StackSettings>(content) : this._serializer.DeserializeYaml<StackSettings>(content);
            }

            return null;
        }

        /// <inheritdoc/>
        public override Task SaveStackSettingsAsync(string stackName, StackSettings settings, CancellationToken cancellationToken = default)
        {
            var settingsName = GetStackSettingsName(stackName);

            var foundExt = ".yaml";
            foreach (var ext in _settingsExtensions)
            {
                var testPath = Path.Combine(this.WorkDir, $"Pulumi.{settingsName}{ext}");
                if (File.Exists(testPath))
                {
                    foundExt = ext;
                    break;
                }
            }

            var path = Path.Combine(this.WorkDir, $"Pulumi.{settingsName}{foundExt}");
            var content = foundExt == ".json" ? this._serializer.SerializeJson(settings) : this._serializer.SerializeYaml(settings);
            return File.WriteAllTextAsync(path, content, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task<ImmutableList<string>> SerializeArgsForOpAsync(string stackName, CancellationToken cancellationToken = default)
            => Task.FromResult(ImmutableList<string>.Empty);

        /// <inheritdoc/>
        public override Task PostCommandCallbackAsync(string stackName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;


        /// <inheritdoc/>
        public override async Task AddEnvironmentsAsync(string stackName, IEnumerable<string> environments, CancellationToken cancellationToken = default)
        {
            CheckSupportsEnvironmentsCommands();
            var args = new List<string> { "config", "env", "add", "--stack", stackName, "--yes" };
            args.AddRange(environments);
            await this.RunCommandAsync(args, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async Task RemoveEnvironmentAsync(string stackName, string environment, CancellationToken cancellationToken = default)
        {
            CheckSupportsEnvironmentsCommands();
            await this.RunCommandAsync(new[] { "config", "env", "rm", environment, "--stack", stackName, "--yes" }, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async Task<string> GetTagAsync(string stackName, string key, CancellationToken cancellationToken = default)
        {
            var result = await this.RunCommandAsync(new[] { "stack", "tag", "get", key, "--stack", stackName }, cancellationToken).ConfigureAwait(false);
            return result.StandardOutput.Trim();
        }

        /// <inheritdoc/>
        public override async Task SetTagAsync(string stackName, string key, string value, CancellationToken cancellationToken = default)
        {
            await this.RunCommandAsync(new[] { "stack", "tag", "set", key, value, "--stack", stackName }, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async Task RemoveTagAsync(string stackName, string key, CancellationToken cancellationToken = default)
        {
            await this.RunCommandAsync(new[] { "stack", "tag", "rm", key, "--stack", stackName }, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async Task<Dictionary<string, string>> ListTagsAsync(string stackName, CancellationToken cancellationToken = default)
        {
            var result = await this.RunCommandAsync(new[] { "stack", "tag", "ls", "--json", "--stack", stackName }, cancellationToken).ConfigureAwait(false);
            return this._serializer.DeserializeJson<Dictionary<string, string>>(result.StandardOutput);
        }

        /// <inheritdoc/>
        public override Task<ConfigValue> GetConfigAsync(string stackName, string key, CancellationToken cancellationToken = default)
            => GetConfigAsync(stackName, key, false, cancellationToken);

        /// <inheritdoc/>
        public override async Task<ConfigValue> GetConfigAsync(string stackName, string key, bool path, CancellationToken cancellationToken = default)
        {
            var args = new List<string> { "config", "get" };
            if (path)
            {
                args.Add("--path");
            }
            args.AddRange(new[] { key, "--json", "--stack", stackName });
            var result = await this.RunCommandAsync(args, cancellationToken).ConfigureAwait(false);
            return this._serializer.DeserializeJson<ConfigValue>(result.StandardOutput);
        }

        /// <inheritdoc/>
        public override async Task<ImmutableDictionary<string, ConfigValue>> GetAllConfigAsync(string stackName, CancellationToken cancellationToken = default)
        {
            var result = await this.RunCommandAsync(new[] { "config", "--show-secrets", "--json", "--stack", stackName }, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(result.StandardOutput))
                return ImmutableDictionary<string, ConfigValue>.Empty;

            var dict = this._serializer.DeserializeJson<Dictionary<string, ConfigValue>>(result.StandardOutput);
            return dict.ToImmutableDictionary();
        }

        /// <inheritdoc/>
        public override Task SetConfigAsync(string stackName, string key, ConfigValue value, CancellationToken cancellationToken = default)
            => SetConfigAsync(stackName, key, value, false, cancellationToken);

        /// <inheritdoc/>
        public override async Task SetConfigAsync(string stackName, string key, ConfigValue value, bool path, CancellationToken cancellationToken = default)
        {
            var args = new List<string> { "config", "set" };
            if (path)
            {
                args.Add("--path");
            }
            var secretArg = value.IsSecret ? "--secret" : "--plaintext";
            args.AddRange(new[] { key, secretArg, "--stack", stackName, "--non-interactive", "--", value.Value });
            await this.RunCommandAsync(args, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override Task SetAllConfigAsync(string stackName, IDictionary<string, ConfigValue> configMap, CancellationToken cancellationToken = default)
            => SetAllConfigAsync(stackName, configMap, false, cancellationToken);

        /// <inheritdoc/>
        public override async Task SetAllConfigAsync(string stackName, IDictionary<string, ConfigValue> configMap, bool path, CancellationToken cancellationToken = default)
        {
            var args = new List<string> { "config", "set-all", "--stack", stackName };
            if (path)
            {
                args.Add("--path");
            }
            foreach (var (key, value) in configMap)
            {
                var secretArg = value.IsSecret ? "--secret" : "--plaintext";
                args.Add(secretArg);
                args.Add($"{key}={value.Value}");
            }
            await this.RunCommandAsync(args, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override Task RemoveConfigAsync(string stackName, string key, CancellationToken cancellationToken = default)
            => RemoveConfigAsync(stackName, key, false, cancellationToken);

        /// <inheritdoc/>
        public override async Task RemoveConfigAsync(string stackName, string key, bool path, CancellationToken cancellationToken = default)
        {
            var args = new List<string> { "config", "rm", key, "--stack", stackName };
            if (path)
            {
                args.Add("--path");
            }
            await this.RunCommandAsync(args, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override Task RemoveAllConfigAsync(string stackName, IEnumerable<string> keys, CancellationToken cancellationToken = default)
            => RemoveAllConfigAsync(stackName, keys, false, cancellationToken);

        /// <inheritdoc/>
        public override async Task RemoveAllConfigAsync(string stackName, IEnumerable<string> keys, bool path, CancellationToken cancellationToken = default)
        {
            var args = new List<string> { "config", "rm-all", "--stack", stackName };
            if (path)
            {
                args.Add("--path");
            }
            args.AddRange(keys);
            await this.RunCommandAsync(args, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async Task<ImmutableDictionary<string, ConfigValue>> RefreshConfigAsync(string stackName, CancellationToken cancellationToken = default)
        {
            await this.RunCommandAsync(new[] { "config", "refresh", "--force", "--stack", stackName }, cancellationToken).ConfigureAwait(false);
            return await this.GetAllConfigAsync(stackName, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async Task<WhoAmIResult> WhoAmIAsync(CancellationToken cancellationToken = default)
        {
            // 3.58 added the --json flag (https://github.com/pulumi/pulumi/releases/tag/v3.58.0)
            if (SupportsCommand(new SemVersion(3, 58)))
            {
                // Use the new --json style
                var result = await this.RunCommandAsync(new[] { "whoami", "--json" }, cancellationToken).ConfigureAwait(false);
                return this._serializer.DeserializeJson<WhoAmIResult>(result.StandardOutput);
            }
            else
            {
                // Fallback to the old just a name style
                var result = await this.RunCommandAsync(new[] { "whoami", }, cancellationToken).ConfigureAwait(false);
                return new WhoAmIResult(result.StandardOutput.Trim(), null, ImmutableArray<string>.Empty);
            }
        }

        /// <inheritdoc/>
        public override Task CreateStackAsync(string stackName, CancellationToken cancellationToken)
        {
            var args = new List<string>
            {
                "stack",
                "init",
                stackName,
            };

            if (!string.IsNullOrWhiteSpace(this.SecretsProvider))
                args.AddRange(new[] { "--secrets-provider", this.SecretsProvider });

            if (Remote)
                args.Add("--no-select");

            return this.RunCommandAsync(args, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task SelectStackAsync(string stackName, CancellationToken cancellationToken)
        {
            // If this is a remote workspace, we don't want to actually select the stack (which would modify
            // global state); but we will ensure the stack exists by calling `pulumi stack`.
            var args = new List<string>
            {
                "stack",
            };
            if (!Remote)
            {
                args.Add("select");
            }
            args.Add("--stack");
            args.Add(stackName);

            return RunCommandAsync(args, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task RemoveStackAsync(string stackName, CancellationToken cancellationToken = default)
            => this.RunCommandAsync(new[] { "stack", "rm", "--yes", stackName }, cancellationToken);

        /// <inheritdoc/>
        public override async Task<ImmutableList<StackSummary>> ListStacksAsync(CancellationToken cancellationToken = default)
        {
            var result = await this.RunCommandAsync(new[] { "stack", "ls", "--json" }, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(result.StandardOutput))
                return ImmutableList<StackSummary>.Empty;

            var stacks = this._serializer.DeserializeJson<List<StackSummary>>(result.StandardOutput);
            return stacks.ToImmutableList();
        }

        /// <inheritdoc/>
        public override async Task<StackDeployment> ExportStackAsync(string stackName, CancellationToken cancellationToken = default)
        {
            var commandResult = await this.RunCommandAsync(
                new[] { "stack", "export", "--stack", stackName, "--show-secrets" },
                cancellationToken).ConfigureAwait(false);
            return StackDeployment.FromJsonString(commandResult.StandardOutput);
        }

        /// <inheritdoc/>
        public override async Task ImportStackAsync(string stackName, StackDeployment state, CancellationToken cancellationToken = default)
        {
            var tempFileName = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tempFileName, state.Json.GetRawText(), cancellationToken).ConfigureAwait(false);
                await this.RunCommandAsync(new[] { "stack", "import", "--file", tempFileName, "--stack", stackName },
                                           cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                File.Delete(tempFileName);
            }
        }

        /// <inheritdoc/>
        public override Task InstallPluginAsync(string name, string version, PluginKind kind = PluginKind.Resource, PluginInstallOptions? options = null, CancellationToken cancellationToken = default)
        {
            var args = new List<string>
            {
                "plugin",
                "install",
                kind.ToString().ToLowerInvariant(),
                name,
                version
            };

            if (options != null)
            {
                if (options.ExactVersion)
                    args.Add("--exact");

                if (!string.IsNullOrWhiteSpace(options.ServerUrl))
                {
                    args.Add("--server");
                    args.Add(options.ServerUrl);
                }
            }

            return this.RunCommandAsync(args, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task RemovePluginAsync(string? name = null, string? versionRange = null, PluginKind kind = PluginKind.Resource, CancellationToken cancellationToken = default)
        {
            var args = new List<string>
            {
                "plugin",
                "rm",
                kind.ToString().ToLowerInvariant(),
            };

            if (!string.IsNullOrWhiteSpace(name))
                args.Add(name);

            if (!string.IsNullOrWhiteSpace(versionRange))
                args.Add(versionRange);

            args.Add("--yes");
            return this.RunCommandAsync(args, cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task<ImmutableList<PluginInfo>> ListPluginsAsync(CancellationToken cancellationToken = default)
        {
            var result = await this.RunCommandAsync(new[] { "plugin", "ls", "--json" }, cancellationToken).ConfigureAwait(false);
            var plugins = this._serializer.DeserializeJson<List<PluginInfo>>(result.StandardOutput);
            return plugins.ToImmutableList();
        }

        /// <inheritdoc/>
        public override async Task<ImmutableDictionary<string, OutputValue>> GetStackOutputsAsync(string stackName, CancellationToken cancellationToken = default)
        {
            // TODO: do this in parallel after this is fixed https://github.com/pulumi/pulumi/issues/6050
            var maskedResult = await this.RunCommandAsync(new[] { "stack", "output", "--json", "--stack", stackName }, cancellationToken).ConfigureAwait(false);
            var plaintextResult = await this.RunCommandAsync(new[] { "stack", "output", "--json", "--show-secrets", "--stack", stackName }, cancellationToken).ConfigureAwait(false);

            var maskedOutput = string.IsNullOrWhiteSpace(maskedResult.StandardOutput)
                ? new Dictionary<string, object>()
                : _serializer.DeserializeJson<Dictionary<string, object>>(maskedResult.StandardOutput);

            var plaintextOutput = string.IsNullOrWhiteSpace(plaintextResult.StandardOutput)
                ? new Dictionary<string, object>()
                : _serializer.DeserializeJson<Dictionary<string, object>>(plaintextResult.StandardOutput);

            var output = new Dictionary<string, OutputValue>();
            foreach (var (key, value) in plaintextOutput)
            {
                var secret = maskedOutput[key] is string maskedValue && maskedValue == "[secret]";
                output[key] = new OutputValue(value, secret);
            }

            return output.ToImmutableDictionary();
        }

        public override Task ChangeSecretsProviderAsync(string stackName, string newSecretsProvider, SecretsProviderOptions? secretsProviderOptions, CancellationToken cancellationToken = default)
        {
            var args = new[] { "stack", "change-secrets-provider", "--stack", stackName, newSecretsProvider };

            if (newSecretsProvider == "passphrase")
            {
                if (string.IsNullOrEmpty(secretsProviderOptions?.NewPassphrase))
                {
                    throw new ArgumentNullException(nameof(secretsProviderOptions), "New passphrase must be set when using passphrase provider.");
                }

                return this.RunInputCommandAsync(args, secretsProviderOptions.NewPassphrase, cancellationToken);
            }

            return this.RunCommandAsync(args, cancellationToken);
        }

        public override Task InstallAsync(InstallOptions? options = default, CancellationToken cancellationToken = default)
        {
            if (!SupportsCommand(new SemVersion(3, 91, 0)))
            {
                throw new InvalidOperationException("The Pulumi CLI version does not support the install command. Please update the Pulumi CLI.");
            };

            var args = new List<string> { "install" };

            if (options is null)
            {
                return this.RunCommandAsync(args, cancellationToken);
            }

            if (options.UseLanguageVersionTools)
            {
                if (!SupportsCommand(new SemVersion(3, 130, 0)))
                {
                    throw new InvalidOperationException($"The Pulumi CLI version does not support {nameof(options.UseLanguageVersionTools)}. Please update the Pulumi CLI.");
                }

                args.Add("--use-language-version-tools");
            }

            if (options.NoDependencies)
                args.Add("--no-dependencies");

            if (options.Reinstall)
                args.Add("--reinstall");

            if (options.NoPlugins)
                args.Add("--no-plugins");

            return this.RunCommandAsync(args, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing
                && this._ownsWorkingDir
                && !string.IsNullOrWhiteSpace(this.WorkDir)
                && Directory.Exists(this.WorkDir))
            {
                try
                {
                    Directory.Delete(this.WorkDir, true);
                }
                catch
                {
                    // allow graceful exit if for some reason
                    // we're not able to delete the directory
                    // will rely on OS to clean temp directory
                    // in this case.
                }
            }
        }

        internal IReadOnlyList<string> GetRemoteArgs()
        {
            if (!Remote)
            {
                return Array.Empty<string>();
            }

            var args = new List<string>
            {
                "--remote"
            };

            if (_remoteGitProgramArgs != null)
            {
                if (!string.IsNullOrEmpty(_remoteGitProgramArgs.Url))
                {
                    args.Add(_remoteGitProgramArgs.Url);
                }
                if (!string.IsNullOrEmpty(_remoteGitProgramArgs.ProjectPath))
                {
                    args.Add("--remote-git-repo-dir");
                    args.Add(_remoteGitProgramArgs.ProjectPath);
                }
                if (!string.IsNullOrEmpty(_remoteGitProgramArgs.Branch))
                {
                    args.Add("--remote-git-branch");
                    args.Add(_remoteGitProgramArgs.Branch);
                }
                if (!string.IsNullOrEmpty(_remoteGitProgramArgs.CommitHash))
                {
                    args.Add("--remote-git-commit");
                    args.Add(_remoteGitProgramArgs.CommitHash);
                }
                if (_remoteGitProgramArgs.Auth != null)
                {
                    if (!string.IsNullOrEmpty(_remoteGitProgramArgs.Auth.PersonalAccessToken))
                    {
                        args.Add("--remote-git-auth-access-token");
                        args.Add(_remoteGitProgramArgs.Auth.PersonalAccessToken);
                    }
                    if (!string.IsNullOrEmpty(_remoteGitProgramArgs.Auth.SshPrivateKey))
                    {
                        args.Add("--remote-git-auth-ssh-private-key");
                        args.Add(_remoteGitProgramArgs.Auth.SshPrivateKey);
                    }
                    if (!string.IsNullOrEmpty(_remoteGitProgramArgs.Auth.SshPrivateKeyPath))
                    {
                        args.Add("--remote-git-auth-ssh-private-key-path");
                        args.Add(_remoteGitProgramArgs.Auth.SshPrivateKeyPath);
                    }
                    if (!string.IsNullOrEmpty(_remoteGitProgramArgs.Auth.Password))
                    {
                        args.Add("--remote-git-auth-password");
                        args.Add(_remoteGitProgramArgs.Auth.Password);
                    }
                    if (!string.IsNullOrEmpty(_remoteGitProgramArgs.Auth.Username))
                    {
                        args.Add("--remote-git-auth-username");
                        args.Add(_remoteGitProgramArgs.Auth.Username);
                    }
                }
            }

            if (_remoteEnvironmentVariables != null)
            {
                foreach (var (name, value) in _remoteEnvironmentVariables)
                {
                    args.Add(value.IsSecret ? "--remote-env-secret" : "--remote-env");
                    args.Add($"{name}={value.Value}");
                }
            }

            if (_remotePreRunCommands != null)
            {
                foreach (var command in _remotePreRunCommands)
                {
                    args.Add("--remote-pre-run-command");
                    args.Add(command);
                }
            }

            if (_remoteSkipInstallDependencies)
            {
                args.Add("--remote-skip-install-dependencies");
            }

            if (_remoteExecutorImage != null)
            {
                args.Add("--remote-executor-image");
                args.Add(_remoteExecutorImage.Image);

                if (_remoteExecutorImage.Credentials != null)
                {
                    args.Add("--remote-executor-image-username");
                    args.Add(_remoteExecutorImage.Credentials.Username);

                    args.Add("--remote-executor-image-password");
                    args.Add(_remoteExecutorImage.Credentials.Password);
                }
            }

            return args;
        }

        private bool SupportsCommand(SemVersion minSupportedVersion)
        {
            var version = _cmd.Version ?? new SemVersion(3, 0);

            return version >= minSupportedVersion;
        }

        private void CheckSupportsEnvironmentsCommands()
        {
            // 3.95 added this command (https://github.com/pulumi/pulumi/releases/tag/v3.95.0)
            if (!SupportsCommand(new SemVersion(3, 95)))
            {
                throw new InvalidOperationException("The Pulumi CLI version does not support env operations on a stack. Please update the Pulumi CLI.");
            }
        }

        // Config options overloads
        public override async Task<ConfigValue> GetConfigWithOptionsAsync(string stackName, string key, ConfigOptions? options = null, CancellationToken cancellationToken = default)
        {
            var args = new List<string> { "config", "get", key, "--json", "--stack", stackName };
            if (options != null)
            {
                if (options.Path) args.Add("--path");
                if (!string.IsNullOrEmpty(options.ConfigFile)) { args.Add("--config-file"); args.Add(options.ConfigFile!); }
                // Note: --show-secrets flag is not supported for 'config get' command,
                // it's only for 'config' command (get all)
            }
            var result = await this.RunCommandAsync(args, null, null, null, null, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return new ConfigValue(string.Empty);
            }

            try
            {
                var jsonOptions = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                // Try deserializing as ConfigValue object with value/secret properties
                var configValue = System.Text.Json.JsonSerializer.Deserialize<ConfigValueModel>(
                    result.StandardOutput, jsonOptions);

                if (configValue != null)
                {
                    return new ConfigValue(configValue.Value ?? string.Empty, configValue.Secret);
                }

                // If that fails, might be a plain string value
                return new ConfigValue(result.StandardOutput.Trim('"'));
            }
            catch (System.Text.Json.JsonException)
            {
                // If JSON parsing fails, return the raw value trimmed
                return new ConfigValue(result.StandardOutput.Trim());
            }
        }

        // Helper class to deserialize JSON response from pulumi config get --json
        private class ConfigValueModel
        {
            public string? Value { get; set; }
            public bool Secret { get; set; }
        }

        public override async Task SetConfigWithOptionsAsync(string stackName, string key, ConfigValue value, ConfigOptions? options = null, CancellationToken cancellationToken = default)
        {
            var args = new List<string> { "config", "set", key, value.Value, "--stack", stackName };
            if (options != null)
            {
                if (options.Path) args.Add("--path");
                if (!string.IsNullOrEmpty(options.ConfigFile)) { args.Add("--config-file"); args.Add(options.ConfigFile!); }
            }
            if (value.IsSecret) args.Add("--secret");
            await this.RunCommandAsync(args, null, null, null, null, cancellationToken).ConfigureAwait(false);
        }

        public override async Task RemoveConfigWithOptionsAsync(string stackName, string key, ConfigOptions? options = null, CancellationToken cancellationToken = default)
        {
            var args = new List<string> { "config", "rm", key, "--stack", stackName };
            if (options != null)
            {
                if (options.Path) args.Add("--path");
                if (!string.IsNullOrEmpty(options.ConfigFile)) { args.Add("--config-file"); args.Add(options.ConfigFile!); }
            }
            await this.RunCommandAsync(args, null, null, null, null, cancellationToken).ConfigureAwait(false);
        }

        public override async Task<ImmutableDictionary<string, ConfigValue>> GetAllConfigWithOptionsAsync(string stackName, GetAllConfigOptions? options = null, CancellationToken cancellationToken = default)
        {
            var args = new List<string> { "config", "--json", "--stack", stackName };
            if (options != null)
            {
                if (options.Path) args.Add("--path");
                if (!string.IsNullOrEmpty(options.ConfigFile)) { args.Add("--config-file"); args.Add(options.ConfigFile!); }
                if (options.ShowSecrets) args.Add("--show-secrets");
            }
            var result = await this.RunCommandAsync(args, null, null, null, null, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return ImmutableDictionary<string, ConfigValue>.Empty;
            }

            try
            {
                // Parse JSON response into Dictionary<string, JsonElement> first to handle various value types
                var jsonOptions = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var configValues = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(
                    result.StandardOutput, jsonOptions);

                if (configValues == null)
                {
                    return ImmutableDictionary<string, ConfigValue>.Empty;
                }

                // Convert each entry to a ConfigValue
                var builder = ImmutableDictionary.CreateBuilder<string, ConfigValue>();
                foreach (var entry in configValues)
                {
                    if (entry.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        // For secret values, look for a structure like {"value":"secretvalue","secret":true}
                        var isSecret = false;
                        var value = string.Empty;

                        if (entry.Value.TryGetProperty("secret", out var secretProp) &&
                            secretProp.ValueKind == System.Text.Json.JsonValueKind.True)
                        {
                            isSecret = true;
                        }

                        if (entry.Value.TryGetProperty("value", out var valueProp) &&
                            valueProp.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            value = valueProp.GetString() ?? string.Empty;
                        }

                        builder.Add(entry.Key, new ConfigValue(value, isSecret));
                    }
                    else if (entry.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        // Plain string value
                        builder.Add(entry.Key, new ConfigValue(entry.Value.GetString() ?? string.Empty));
                    }
                    else
                    {
                        // Convert any other value to string
                        builder.Add(entry.Key, new ConfigValue(entry.Value.ToString()));
                    }
                }

                return builder.ToImmutable();
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new InvalidOperationException($"Could not parse config values: {ex.Message}");
            }
        }

        public override async Task SetAllConfigWithOptionsAsync(string stackName, IDictionary<string, ConfigValue> configMap, ConfigOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (configMap == null || !configMap.Any())
            {
                return;
            }

            var args = new List<string> { "config", "set-all", "--stack", stackName };
            if (options != null)
            {
                if (options.Path) args.Add("--path");
                if (!string.IsNullOrEmpty(options.ConfigFile)) { args.Add("--config-file"); args.Add(options.ConfigFile!); }
            }

            // For each config value, add as either secret or plaintext
            foreach (var kvp in configMap)
            {
                args.Add(kvp.Value.IsSecret ? "--secret" : "--plaintext");
                args.Add($"{kvp.Key}={kvp.Value.Value}");
            }

            await this.RunCommandAsync(args, null, null, null, null, cancellationToken).ConfigureAwait(false);
        }

        public override async Task RemoveAllConfigWithOptionsAsync(string stackName, IEnumerable<string> keys, ConfigOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (keys == null || !keys.Any())
            {
                return;
            }

            var args = new List<string> { "config", "rm-all", "--stack", stackName };
            if (options != null)
            {
                if (options.Path) args.Add("--path");
                if (!string.IsNullOrEmpty(options.ConfigFile)) { args.Add("--config-file"); args.Add(options.ConfigFile!); }
            }

            // Add all keys to remove
            args.AddRange(keys);

            await this.RunCommandAsync(args, null, null, null, null, cancellationToken).ConfigureAwait(false);
        }
    }
}

// Copyright 2016-2022, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pulumi.Automation.Commands;
using Pulumi.Automation.Commands.Exceptions;
using Pulumi.Automation.Events;
using Pulumi.Automation.Exceptions;
using Pulumi.Automation.Serialization;

namespace Pulumi.Automation
{
    /// <summary>
    /// <see cref="WorkspaceStack"/> is an isolated, independently configurable instance of a
    /// Pulumi program. <see cref="WorkspaceStack"/> exposes methods for the full pulumi lifecycle
    /// (up/preview/refresh/destroy), as well as managing configuration.
    /// <para/>
    /// Multiple stacks are commonly used to denote different phases of development
    /// (such as development, staging, and production) or feature branches (such as
    /// feature-x-dev, jane-feature-x-dev).
    /// <para/>
    /// Will dispose the <see cref="Workspace"/> on <see cref="Dispose"/>.
    /// </summary>
    public sealed class WorkspaceStack : IDisposable
    {
        private readonly Task _readyTask;

        /// <summary>
        /// The name identifying the Stack.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The Workspace the Stack was created from.
        /// </summary>
        public Workspace Workspace { get; }

        /// <summary>
        /// A module for editing the Stack's state.
        /// </summary>
        public WorkspaceStackState State { get; }

        /// <summary>
        /// Creates a new stack using the given workspace, and stack name.
        /// It fails if a stack with that name already exists.
        /// </summary>
        /// <param name="name">The name identifying the stack.</param>
        /// <param name="workspace">The Workspace the Stack was created from.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <exception cref="StackAlreadyExistsException">If a stack with the provided name already exists.</exception>
        public static async Task<WorkspaceStack> CreateAsync(
            string name,
            Workspace workspace,
            CancellationToken cancellationToken = default)
        {
            var stack = new WorkspaceStack(name, workspace, WorkspaceStackInitMode.Create, cancellationToken);
            await stack._readyTask.ConfigureAwait(false);
            return stack;
        }

        /// <summary>
        /// Selects stack using the given workspace, and stack name.
        /// It returns an error if the given Stack does not exist.
        /// </summary>
        /// <param name="name">The name identifying the stack.</param>
        /// <param name="workspace">The Workspace the Stack was created from.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <exception cref="StackNotFoundException">If a stack with the provided name does not exists.</exception>
        public static async Task<WorkspaceStack> SelectAsync(
            string name,
            Workspace workspace,
            CancellationToken cancellationToken = default)
        {
            var stack = new WorkspaceStack(name, workspace, WorkspaceStackInitMode.Select, cancellationToken);
            await stack._readyTask.ConfigureAwait(false);
            return stack;
        }

        /// <summary>
        /// Tries to create a new Stack using the given workspace, and stack name
        /// if the stack does not already exist, or falls back to selecting an
        /// existing stack. If the stack does not exist, it will be created and
        /// selected.
        /// </summary>
        /// <param name="name">The name of the identifying stack.</param>
        /// <param name="workspace">The Workspace the Stack was created from.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public static async Task<WorkspaceStack> CreateOrSelectAsync(
            string name,
            Workspace workspace,
            CancellationToken cancellationToken = default)
        {
            var stack = new WorkspaceStack(name, workspace, WorkspaceStackInitMode.CreateOrSelect, cancellationToken);
            await stack._readyTask.ConfigureAwait(false);
            return stack;
        }

        private WorkspaceStack(
            string name,
            Workspace workspace,
            WorkspaceStackInitMode mode,
            CancellationToken cancellationToken)
        {
            this.Name = name;
            this.Workspace = workspace;
            this.State = new WorkspaceStackState(this);

            this._readyTask = mode switch
            {
                WorkspaceStackInitMode.Create => workspace.CreateStackAsync(name, cancellationToken),
                WorkspaceStackInitMode.Select => workspace.SelectStackAsync(name, cancellationToken),
                WorkspaceStackInitMode.CreateOrSelect => Task.Run(async () =>
                {
                    try
                    {
                        await workspace.SelectStackAsync(name, cancellationToken).ConfigureAwait(false);
                    }
                    catch (StackNotFoundException)
                    {
                        await workspace.CreateStackAsync(name, cancellationToken).ConfigureAwait(false);
                    }
                }),
                _ => throw new InvalidOperationException($"Unexpected Stack creation mode: {mode}")
            };
        }

        /// <summary>
        /// Returns the value associated with the stack and key, scoped to the Workspace.
        /// </summary>
        /// <param name="key">The key to use for the tag lookup.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public Task<string> GetTagAsync(string key, CancellationToken cancellationToken = default)
            => this.Workspace.GetTagAsync(this.Name, key, cancellationToken);

        /// <summary>
        /// Sets the specified key-value pair on the stack.
        /// </summary>
        /// <param name="key">The tag key to set.</param>
        /// <param name="value">The tag value to set.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public Task SetTagAsync(string key, string value, CancellationToken cancellationToken = default)
            => this.Workspace.SetTagAsync(this.Name, key, value, cancellationToken);

        /// <summary>
        /// Sets the specified key-value pair on the stack.
        /// </summary>
        /// <param name="key">The tag key to set.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public Task RemoveTagAsync(string key, CancellationToken cancellationToken = default)
            => this.Workspace.RemoveTagAsync(this.Name, key, cancellationToken);

        /// <summary>
        /// Returns the tag map for the stack, scoped to the current Workspace.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        public Task<Dictionary<string, string>> ListTagsAsync(CancellationToken cancellationToken = default)
            => this.Workspace.ListTagsAsync(this.Name, cancellationToken);

        /// <summary>
        /// Returns the config value associated with the specified key.
        /// </summary>
        /// <param name="key">The key to use for the config lookup.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public Task<ConfigValue> GetConfigAsync(string key, CancellationToken cancellationToken = default)
            => GetConfigAsync(key, false, cancellationToken);

        /// <summary>
        /// Returns the config value associated with the specified key.
        /// </summary>
        /// <param name="key">The key to use for the config lookup.</param>
        /// <param name="path">The key contains a path to a property in a map or list to get.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public Task<ConfigValue> GetConfigAsync(string key, bool path, CancellationToken cancellationToken = default)
            => this.Workspace.GetConfigAsync(this.Name, key, path, cancellationToken);

        /// <summary>
        /// Returns the full config map associated with the stack in the Workspace.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        public Task<ImmutableDictionary<string, ConfigValue>> GetAllConfigAsync(CancellationToken cancellationToken = default)
            => this.Workspace.GetAllConfigAsync(this.Name, cancellationToken);

        /// <summary>
        /// Sets the config key-value pair on the Stack in the associated Workspace.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="value">The config value to set.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public Task SetConfigAsync(string key, ConfigValue value, CancellationToken cancellationToken = default)
            => SetConfigAsync(key, value, false, cancellationToken);

        /// <summary>
        /// Sets the config key-value pair on the Stack in the associated Workspace.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="value">The config value to set.</param>
        /// <param name="path">The key contains a path to a property in a map or list to set.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public Task SetConfigAsync(string key, ConfigValue value, bool path, CancellationToken cancellationToken = default)
            => this.Workspace.SetConfigAsync(this.Name, key, value, path, cancellationToken);

        /// <summary>
        /// Sets all specified config values on the stack in the associated Workspace.
        /// </summary>
        /// <param name="configMap">The map of config key-value pairs to set.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public Task SetAllConfigAsync(IDictionary<string, ConfigValue> configMap, CancellationToken cancellationToken = default)
            => SetAllConfigAsync(configMap, false, cancellationToken);

        /// <summary>
        /// Sets all specified config values on the stack in the associated Workspace.
        /// </summary>
        /// <param name="configMap">The map of config key-value pairs to set.</param>
        /// <param name="path">The keys contain a path to a property in a map or list to set.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public Task SetAllConfigAsync(IDictionary<string, ConfigValue> configMap, bool path, CancellationToken cancellationToken = default)
            => this.Workspace.SetAllConfigAsync(this.Name, configMap, path, cancellationToken);

        /// <summary>
        /// Removes the specified config key from the Stack in the associated Workspace.
        /// </summary>
        /// <param name="key">The config key to remove.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public Task RemoveConfigAsync(string key, CancellationToken cancellationToken = default)
            => RemoveConfigAsync(key, false, cancellationToken);

        /// <summary>
        /// Removes the specified config key from the Stack in the associated Workspace.
        /// </summary>
        /// <param name="key">The config key to remove.</param>
        /// <param name="path">The key contains a path to a property in a map or list to remove.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public Task RemoveConfigAsync(string key, bool path, CancellationToken cancellationToken = default)
            => this.Workspace.RemoveConfigAsync(this.Name, key, path, cancellationToken);

        /// <summary>
        /// Removes the specified config keys from the Stack in the associated Workspace.
        /// </summary>
        /// <param name="keys">The config keys to remove.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public Task RemoveAllConfigAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
            => RemoveAllConfigAsync(keys, false, cancellationToken);

        /// <summary>
        /// Removes the specified config keys from the Stack in the associated Workspace.
        /// </summary>
        /// <param name="keys">The config keys to remove.</param>
        /// <param name="path">The keys contain a path to a property in a map or list to remove.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public Task RemoveAllConfigAsync(IEnumerable<string> keys, bool path, CancellationToken cancellationToken = default)
            => this.Workspace.RemoveAllConfigAsync(this.Name, keys, path, cancellationToken);

        /// <summary>
        /// Gets and sets the config map used with the last update.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        public Task<ImmutableDictionary<string, ConfigValue>> RefreshConfigAsync(CancellationToken cancellationToken = default)
            => this.Workspace.RefreshConfigAsync(this.Name, cancellationToken);


        /// <summary>
        /// Adds environments to the end of a stack's import list. Imported environments are merged in order
        /// per the ESC merge rules. The list of environments behaves as if it were the import list in an anonymous
        /// environment.
        /// </summary>
        /// <param name="environments">List of environments to add to the end of the stack's import list.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public Task AddEnvironmentsAsync(IEnumerable<string> environments, CancellationToken cancellationToken = default)
            => this.Workspace.AddEnvironmentsAsync(this.Name, environments, cancellationToken);

        /// <summary>
        /// Removes environments from a stack's import list.
        /// </summary>
        /// <param name="environment">The name of the environment to remove from the stack's configuration.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public Task RemoveEnvironmentAsync(string environment, CancellationToken cancellationToken = default)
            => this.Workspace.RemoveEnvironmentAsync(this.Name, environment, cancellationToken);

        /// <summary>
        /// Creates or updates the resources in a stack by executing the program in the Workspace.
        /// <para/>
        /// https://www.pulumi.com/docs/reference/cli/pulumi_up/
        /// </summary>
        /// <param name="options">Options to customize the behavior of the update.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async Task<UpResult> UpAsync(
            UpOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var execKind = ExecKind.Local;
            var program = this.Workspace.Program;
            var logger = this.Workspace.Logger;
            var args = new List<string>()
            {
                "up",
                "--yes",
                "--skip-preview",
            };

            args.AddRange(GetRemoteArgs());

            if (options != null)
            {
                if (options.Program != null)
                    program = options.Program;

                if (options.Logger != null)
                    logger = options.Logger;

                if (options.ExpectNoChanges is true)
                    args.Add("--expect-no-changes");

                if (options.Diff is true)
                    args.Add("--diff");

                if (options.Plan != null)
                {
                    args.Add("--plan");
                    args.Add(options.Plan);
                }

                if (options.Replace?.Any() == true)
                {
                    foreach (var item in options.Replace)
                    {
                        args.Add("--replace");
                        args.Add(item);
                    }
                }

                if (options.TargetDependents is true)
                    args.Add("--target-dependents");

                if (options.ContinueOnError is true)
                    args.Add("--continue-on-error");

                ApplyUpdateOptions(options, args);
            }

            InlineLanguageHost? inlineHost = null;

            try
            {
                if (program != null)
                {
                    execKind = ExecKind.Inline;
                    inlineHost = new InlineLanguageHost(program, logger, cancellationToken);
                    await inlineHost.StartAsync().ConfigureAwait(false);
                    var port = await inlineHost.GetPortAsync().ConfigureAwait(false);
                    args.Add($"--client=127.0.0.1:{port}");
                }

                args.Add("--exec-kind");
                args.Add(execKind);

                CommandResult upResult;
                try
                {
                    upResult = await this.RunCommandAsync(
                        args,
                        options?.OnStandardOutput,
                        options?.OnStandardError,
                        options?.OnEvent,
                        cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    if (inlineHost != null && inlineHost.TryGetExceptionInfo(out var exceptionInfo))
                        exceptionInfo.Throw();

                    // this won't be hit if we have an inline
                    // program exception
                    throw;
                }

                var output = await this.GetOutputsAsync(cancellationToken).ConfigureAwait(false);
                // If it's a remote workspace, explicitly set showSecrets to false to prevent attempting to
                // load the project file.
                var showSecrets = Remote ? false : options?.ShowSecrets;
                var summary = await this.GetInfoAsync(cancellationToken, showSecrets).ConfigureAwait(false);
                return new UpResult(
                    upResult.StandardOutput,
                    upResult.StandardError,
                    summary!,
                    output);
            }
            finally
            {
                if (inlineHost != null)
                {
                    await inlineHost.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Performs a dry-run update to a stack, returning pending changes.
        /// <para/>
        /// https://www.pulumi.com/docs/reference/cli/pulumi_preview/
        /// </summary>
        /// <param name="options">Options to customize the behavior of the update.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async Task<PreviewResult> PreviewAsync(
            PreviewOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var execKind = ExecKind.Local;
            var program = this.Workspace.Program;
            var logger = this.Workspace.Logger;
            var args = new List<string>() { "preview" };

            args.AddRange(GetRemoteArgs());

            if (options != null)
            {
                if (options.Program != null)
                    program = options.Program;

                if (options.Logger != null)
                    logger = options.Logger;

                if (options.ExpectNoChanges is true)
                    args.Add("--expect-no-changes");

                if (options.Diff is true)
                    args.Add("--diff");

                if (options.Plan != null)
                {
                    args.Add("--save-plan");
                    args.Add(options.Plan);
                }

                if (options.Replace?.Any() == true)
                {
                    foreach (var item in options.Replace)
                    {
                        args.Add("--replace");
                        args.Add(item);
                    }
                }

                if (options.TargetDependents is true)
                    args.Add("--target-dependents");


                ApplyUpdateOptions(options, args);
            }

            InlineLanguageHost? inlineHost = null;

            SummaryEvent? summaryEvent = null;

            var onEvent = options?.OnEvent;

            void OnPreviewEvent(EngineEvent @event)
            {
                if (@event.SummaryEvent != null)
                {
                    summaryEvent = @event.SummaryEvent;
                }

                onEvent?.Invoke(@event);
            }

            try
            {
                if (program != null)
                {
                    execKind = ExecKind.Inline;
                    inlineHost = new InlineLanguageHost(program, logger, cancellationToken);
                    await inlineHost.StartAsync().ConfigureAwait(false);
                    var port = await inlineHost.GetPortAsync().ConfigureAwait(false);
                    args.Add($"--client=127.0.0.1:{port}");
                }

                args.Add("--exec-kind");
                args.Add(execKind);

                CommandResult result;
                try
                {
                    result = await this.RunCommandAsync(
                        args,
                        options?.OnStandardOutput,
                        options?.OnStandardError,
                        OnPreviewEvent,
                        cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    if (inlineHost != null && inlineHost.TryGetExceptionInfo(out var exceptionInfo))
                        exceptionInfo.Throw();

                    // this won't be hit if we have an inline
                    // program exception
                    throw;
                }

                if (summaryEvent is null)
                {
                    throw new NoSummaryEventException("No summary of changes for 'preview'");
                }

                return new PreviewResult(
                    result.StandardOutput,
                    result.StandardError,
                    summaryEvent.ResourceChanges);
            }
            finally
            {
                if (inlineHost != null)
                {
                    await inlineHost.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Compares the current stack’s resource state with the state known to exist in the actual
        /// cloud provider. Any such changes are adopted into the current stack.
        /// </summary>
        /// <param name="options">Options to customize the behavior of the refresh.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async Task<UpdateResult> RefreshAsync(
            RefreshOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var args = new List<string>
            {
                "refresh",
                "--yes",
                "--skip-preview",
            };

            args.AddRange(GetRemoteArgs());

            if (options != null)
            {
                if (options.ExpectNoChanges is true)
                    args.Add("--expect-no-changes");

                if (options.SkipPendingCreates is true)
                    args.Add("--skip-pending-creates");

                if (options.ClearPendingCreates is true)
                    args.Add("--clear-pending-creates");

                if (options.ImportPendingCreates?.Any() == true)
                {
                    foreach (var item in options.ImportPendingCreates)
                    {
                        args.Add("--import-pending-creates");
                        args.Add(item.Urn);
                        args.Add("--import-pending-creates");
                        args.Add(item.Id);
                    }
                }

                ApplyUpdateOptions(options, args);
            }

            var execKind = Workspace.Program is null ? ExecKind.Local : ExecKind.Inline;
            args.Add("--exec-kind");
            args.Add(execKind);

            var result = await this.RunCommandAsync(args, options?.OnStandardOutput, options?.OnStandardError, options?.OnEvent, cancellationToken).ConfigureAwait(false);
            // If it's a remote workspace, explicitly set showSecrets to false to prevent attempting to
            // load the project file.
            var showSecrets = Remote ? false : options?.ShowSecrets;
            var summary = await this.GetInfoAsync(cancellationToken, showSecrets).ConfigureAwait(false);
            return new UpdateResult(
                result.StandardOutput,
                result.StandardError,
                summary!);
        }

        /// <summary>
        /// Destroy deletes all resources in a stack, leaving all history and configuration intact.
        /// </summary>
        /// <param name="options">Options to customize the behavior of the destroy.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async Task<UpdateResult> DestroyAsync(
            DestroyOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var args = new List<string>
            {
                "destroy",
                "--yes",
                "--skip-preview",
            };

            args.AddRange(GetRemoteArgs());

            if (options != null)
            {
                if (options.TargetDependents is true)
                    args.Add("--target-dependents");

                if (options.ContinueOnError is true)
                    args.Add("--continue-on-error");

                ApplyUpdateOptions(options, args);
            }

            var execKind = Workspace.Program is null ? ExecKind.Local : ExecKind.Inline;
            args.Add("--exec-kind");
            args.Add(execKind);

            var result = await this.RunCommandAsync(args, options?.OnStandardOutput, options?.OnStandardError, options?.OnEvent, cancellationToken).ConfigureAwait(false);
            // If it's a remote workspace, explicitly set showSecrets to false to prevent attempting to
            // load the project file.
            var showSecrets = Remote ? false : options?.ShowSecrets;
            var summary = await this.GetInfoAsync(cancellationToken, showSecrets).ConfigureAwait(false);
            return new UpdateResult(
                result.StandardOutput,
                result.StandardError,
                summary!);
        }

        /// <summary>
        /// Import resources into a stack.
        /// </summary>
        /// <param name="options">Import options to customize the behaviour of the import operation</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The output of the import command, including the generated code, if any.</returns>
        public async Task<ImportResult> ImportAsync(
            ImportOptions options,
            CancellationToken cancellationToken = default)
        {
            var args = new List<string>
            {
                "import",
                "--yes",
                "--skip-preview",
            };

            var tempDirectoryPath = "";
            try
            {
                // for import operations, generate a temporary directory to store the following:
                //   - the import file when the user specifies resources to import
                //   - the output file for the generated code
                // we use the import file as input to the import command
                // we the output file to read the generated code and return it to the user
                tempDirectoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

                if (options.Resources?.Any() == true)
                {
                    Directory.CreateDirectory(tempDirectoryPath);
                    var importPath = Path.Combine(tempDirectoryPath, "import.json");
                    var importContent = new
                    {
                        nameTable = options.NameTable,
                        resources = options.Resources,
                    };

                    var importJson = JsonSerializer.Serialize(importContent, new JsonSerializerOptions
                    {
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    await File.WriteAllTextAsync(importPath, importJson, cancellationToken);
                    args.Add("--file");
                    args.Add(importPath);
                }

                if (options.Protect is false)
                {
                    args.Add("--protect=false");
                }

                var generatedCodeOutputPath = Path.Combine(tempDirectoryPath, "generated.cs");
                if (options.GenerateCode is false)
                {
                    args.Add("--generate-code=false");
                }
                else
                {
                    args.Add("--out");
                    args.Add(generatedCodeOutputPath);
                }

                if (options.Converter != null)
                {
                    // if the user specifies a converter, pass it to `--from <converter>` argument of import.
                    args.Add("--from");
                    args.Add(options.Converter);
                    if (options.ConverterArgs?.Any() == true)
                    {
                        // pass any additional arguments to the converter
                        args.Add("--");
                        args.AddRange(options.ConverterArgs);
                    }
                }

                var result = await this.RunCommandAsync(args, options.OnStandardOutput, options.OnStandardError, null, cancellationToken).ConfigureAwait(false);
                var summary = await this.GetInfoAsync(cancellationToken, options.ShowSecrets).ConfigureAwait(false);
                var generatedCode =
                    options.GenerateCode is not false
                        ? await File.ReadAllTextAsync(generatedCodeOutputPath, cancellationToken).ConfigureAwait(false)
                        : "";

                return new ImportResult(
                    standardOutput: result.StandardOutput,
                    standardError: result.StandardError,
                    summary: summary!,
                    generatedCode: generatedCode);
            }
            finally
            {
                if (tempDirectoryPath != "")
                {
                    // clean up the temporary directory we used for the import operation
                    Directory.Delete(tempDirectoryPath, recursive: true);
                }
            }
        }

        /// <summary>
        /// Gets the current set of Stack outputs from the last <see cref="UpAsync(UpOptions?, CancellationToken)"/>.
        /// </summary>
        public Task<ImmutableDictionary<string, OutputValue>> GetOutputsAsync(CancellationToken cancellationToken = default)
            => this.Workspace.GetStackOutputsAsync(this.Name, cancellationToken);

        /// <summary>
        /// Returns a list summarizing all previews and current results from Stack lifecycle operations (up/preview/refresh/destroy).
        /// </summary>
        /// <param name="options">Options to customize the behavior of the fetch history action.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async Task<ImmutableList<UpdateSummary>> GetHistoryAsync(
            HistoryOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var args = new List<string>
            {
                "stack",
                "history",
                "--json",
            };

            if (options?.ShowSecrets ?? true)
            {
                args.Add("--show-secrets");
            }

            if (options?.PageSize.HasValue == true)
            {
                if (options.PageSize!.Value < 1)
                    throw new ArgumentException($"{nameof(options.PageSize)} must be greater than or equal to 1.", nameof(options.PageSize));

                var page = !options.Page.HasValue ? 1
                    : options.Page.Value < 1 ? 1
                    : options.Page.Value;

                args.Add("--page-size");
                args.Add(options.PageSize.Value.ToString());
                args.Add("--page");
                args.Add(page.ToString());
            }

            var result = await this.RunCommandAsync(args, null, null, null, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(result.StandardOutput))
                return ImmutableList<UpdateSummary>.Empty;

            var jsonOptions = LocalSerializer.BuildJsonSerializerOptions();
            var list = JsonSerializer.Deserialize<List<UpdateSummary>>(result.StandardOutput, jsonOptions)!;
            return list.ToImmutableList();
        }

        /// <summary>
        /// Exports the deployment state of the stack.
        /// <para/>
        /// This can be combined with ImportStackAsync to edit a
        /// stack's state (such as recovery from failed deployments).
        /// </summary>
        public Task<StackDeployment> ExportStackAsync(CancellationToken cancellationToken = default)
            => this.Workspace.ExportStackAsync(this.Name, cancellationToken);

        /// <summary>
        /// Imports the specified deployment state into a pre-existing stack.
        /// <para/>
        /// This can be combined with ExportStackAsync to edit a
        /// stack's state (such as recovery from failed deployments).
        /// </summary>
        public Task ImportStackAsync(StackDeployment state, CancellationToken cancellationToken = default)
            => this.Workspace.ImportStackAsync(this.Name, state, cancellationToken);

        public async Task<UpdateSummary?> GetInfoAsync(CancellationToken cancellationToken = default)
        {
            return await GetInfoAsync(cancellationToken, true);
        }

        private async Task<UpdateSummary?> GetInfoAsync(CancellationToken cancellationToken = default, bool? showSecrets = default)
        {
            var history = await this.GetHistoryAsync(
                new HistoryOptions
                {
                    PageSize = 1,
                    ShowSecrets = showSecrets,
                },
                cancellationToken).ConfigureAwait(false);

            return history.FirstOrDefault();
        }

        /// <summary>
        /// Cancel stops a stack's currently running update. It throws
        /// an exception if no update is currently running. Note that
        /// this operation is _very dangerous_, and may leave the
        /// stack in an inconsistent state if a resource operation was
        /// pending when the update was canceled. This command is not
        /// supported for local backends.
        /// </summary>
        public async Task CancelAsync(CancellationToken cancellationToken = default)
            => await this.Workspace.RunCommandAsync(new[] { "cancel", "--stack", this.Name, "--yes" }, cancellationToken).ConfigureAwait(false);

        internal async Task<CommandResult> RunCommandAsync(
            IList<string> args,
            Action<string>? onStandardOutput,
            Action<string>? onStandardError,
            Action<EngineEvent>? onEngineEvent,
            CancellationToken cancellationToken)
        {
            args = args.Concat(new[] { "--stack", this.Name }).ToList();
            return await this.Workspace.RunStackCommandAsync(this.Name, args, onStandardOutput, onStandardError, onEngineEvent, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
            => this.Workspace.Dispose();

        private static class ExecKind
        {
            public const string Local = "auto.local";
            public const string Inline = "auto.inline";
        }

        private enum WorkspaceStackInitMode
        {
            Create,
            Select,
            CreateOrSelect
        }

        private class InlineLanguageHost : IAsyncDisposable
        {
            private readonly TaskCompletionSource<int> _portTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly CancellationToken _cancelToken;
            private readonly IHost _host;
            private readonly CancellationTokenRegistration _portRegistration;

            public InlineLanguageHost(
                PulumiFn program,
                ILogger? logger,
                CancellationToken cancellationToken)
            {
                this._cancelToken = cancellationToken;
                this._host = Host.CreateDefaultBuilder()
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder
                            .ConfigureKestrel(kestrelOptions =>
                            {
                                kestrelOptions.Listen(IPAddress.Loopback, 0, listenOptions =>
                                {
                                    listenOptions.Protocols = HttpProtocols.Http2;
                                });
                            })
                            .ConfigureAppConfiguration((context, config) =>
                            {
                                // clear so we don't read appsettings.json
                                // note that we also won't read environment variables for config
                                config.Sources.Clear();
                            })
                            .ConfigureLogging(loggingBuilder =>
                            {
                                // disable default logging
                                loggingBuilder.ClearProviders();
                            })
                            .ConfigureServices(services =>
                            {
                                // to be injected into LanguageRuntimeService
                                var callerContext = new LanguageRuntimeService.CallerContext(program, logger, cancellationToken);
                                services.AddSingleton(callerContext);

                                services.AddGrpc(grpcOptions =>
                                {
                                    grpcOptions.MaxReceiveMessageSize = LanguageRuntimeService.MaxRpcMesageSize;
                                    grpcOptions.MaxSendMessageSize = LanguageRuntimeService.MaxRpcMesageSize;
                                });
                            })
                            .Configure(app =>
                            {
                                app.UseRouting();
                                app.UseEndpoints(endpoints =>
                                {
                                    endpoints.MapGrpcService<LanguageRuntimeService>();
                                });
                            });
                    })
                    .Build();

                // before starting the host, set up this callback to tell us what port was selected
                this._portRegistration = this._host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(() =>
                {
                    try
                    {
                        var serverFeatures = this._host.Services.GetRequiredService<IServer>().Features;
                        var addresses = serverFeatures.Get<IServerAddressesFeature>()!.Addresses.ToList();
                        Debug.Assert(addresses.Count == 1, "Server should only be listening on one address");
                        var uri = new Uri(addresses[0]);
                        this._portTcs.TrySetResult(uri.Port);
                    }
                    catch (Exception ex)
                    {
                        this._portTcs.TrySetException(ex);
                    }
                });
            }

            public Task StartAsync()
                => this._host.StartAsync(this._cancelToken);

            public Task<int> GetPortAsync()
                => this._portTcs.Task;

            public bool TryGetExceptionInfo([NotNullWhen(true)] out ExceptionDispatchInfo? info)
            {
                var callerContext = this._host.Services.GetRequiredService<LanguageRuntimeService.CallerContext>();
                if (callerContext.ExceptionDispatchInfo is null)
                {
                    info = null;
                    return false;
                }

                info = callerContext.ExceptionDispatchInfo;
                return true;
            }

            public async ValueTask DisposeAsync()
            {
                this._portRegistration.Unregister();
                await this._host.StopAsync(this._cancelToken).ConfigureAwait(false);
                this._host.Dispose();
            }
        }

        static void ApplyUpdateOptions(UpdateOptions options, List<string> args)
        {
            if (options.Parallel.HasValue)
            {
                args.Add("--parallel");
                args.Add(options.Parallel.Value.ToString());
            }

            if (!string.IsNullOrWhiteSpace(options.Message))
            {
                args.Add("--message");
                args.Add(options.Message);
            }

            if (options.Target?.Any() == true)
            {
                foreach (var item in options.Target)
                {
                    args.Add("--target");
                    args.Add(item);
                }
            }

            if (options.PolicyPacks?.Any() == true)
            {
                foreach (var item in options.PolicyPacks)
                {
                    args.Add("--policy-pack");
                    args.Add(item);
                }
            }

            if (options.PolicyPackConfigs?.Any() == true)
            {
                foreach (var item in options.PolicyPackConfigs)
                {
                    args.Add("--policy-pack-configs");
                    args.Add(item);
                }
            }

            if (!string.IsNullOrWhiteSpace(options.Color))
            {
                args.Add("--color");
                args.Add(options.Color);
            }

            if (options.LogFlow is true)
            {
                args.Add("--logflow");
            }

            if (options.LogVerbosity.HasValue)
            {
                args.Add("--verbose");
                args.Add(options.LogVerbosity.Value.ToString());
            }

            if (options.LogToStdErr is true)
            {
                args.Add("--logtostderr");
            }

            if (!string.IsNullOrWhiteSpace(options.Tracing))
            {
                args.Add("--tracing");
                args.Add(options.Tracing);
            }

            if (options.Debug is true)
            {
                args.Add("--debug");
            }

            if (options.Json is true)
            {
                args.Add("--json");
            }
        }

        private bool Remote
            => Workspace is LocalWorkspace localWorkspace && localWorkspace.Remote;

        private IReadOnlyList<string> GetRemoteArgs()
            => Workspace is LocalWorkspace localWorkspace ? localWorkspace.GetRemoteArgs() : Array.Empty<string>();
    }
}

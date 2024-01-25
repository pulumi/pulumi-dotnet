// Copyright 2016-2021, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Semver;
using CliWrap;
using CliWrap.Buffered;
using Pulumi.Automation.Commands.Exceptions;
using Pulumi.Automation.Events;


namespace Pulumi.Automation.Commands
{
    /// <summary>
    /// Options to configure a <see cref="LocalPulumiCmd"/> instance.
    /// </summary>
    public class LocalPulumiCmdOptions
    {
        /// <summary>
        /// The version of the Pulumi CLI to install or the minimum version requirement for an existing installation.
        /// </summary>
        public SemVersion? Version { get; set; }
        /// <summary>
        /// The directory where to install the Pulumi CLI to or where to find an existing installation.
        /// </summary>
        public string? Root { get; set; }
        /// <summary>
        /// If true, skips the version validation that checks if an existing Pulumi CLI installation is compatible with the SDK.
        /// </summary>
        public bool SkipVersionCheck { get; set; }
    }

    /// <summary>
    /// A <see cref="IPulumiCmd"/> implementation that uses a locally installed Pulumi CLI.
    /// </summary>
    public class LocalPulumiCmd : IPulumiCmd
    {
        // TODO: move to shared place with LocalWorkspace
        private static string SkipVersionCheckVar = "PULUMI_AUTOMATION_API_SKIP_VERSION_CHECK";
        private static readonly SemVersion _minimumVersion = new SemVersion(3, 1, 0);
        private readonly string _command;
        private SemVersion? _version;

        internal Task ReadyTask { get; }

        /// <summary>
        /// The version of the Pulumi CLI that is being used.
        /// </summary>
        public SemVersion? Version { get => _version; }

        /// <summary>
        /// Creates a new LocalPulumiCmd instance.
        /// </summary>
        /// <param name="options">Options to configure the LocalPulumiCmd.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns></returns>
        public static async Task<LocalPulumiCmd> CreateAsync(
            LocalPulumiCmdOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var cmd = new LocalPulumiCmd(options, cancellationToken);
            await cmd.ReadyTask.ConfigureAwait(false);
            return cmd;
        }

        private LocalPulumiCmd(LocalPulumiCmdOptions? options, CancellationToken cancellationToken)
        {
            var readyTasks = new List<Task>();

            if (options?.Root != null)
            {
                _command = Path.Combine(options.Root, "bin", "pulumi");
            }
            else
            {
                _command = "pulumi";
            }

            var minimumVersion = _minimumVersion;
            if (options?.Version != null && options.Version > minimumVersion)
            {
                minimumVersion = options.Version;
            }

            var optOut = options?.SkipVersionCheck ?? Environment.GetEnvironmentVariable(SkipVersionCheckVar) != null;
            readyTasks.Add(SetPulumiVersionAsync(minimumVersion, _command, optOut, cancellationToken));
            ReadyTask = Task.WhenAll(readyTasks);
        }

        private async Task SetPulumiVersionAsync(SemVersion minimumVersion, string command, bool optOut, CancellationToken cancellationToken)
        {
            var result = await Cli.Wrap(command)
                .WithArguments("version")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new Exception($"failed to get pulumi version: {result.StandardOutput ?? ""}");
            }
            var version = result.StandardOutput.Trim().TrimStart('v');
            _version = ParseAndValidatePulumiVersion(minimumVersion, version, optOut);
        }

        /// <summary>
        /// Installs the Pulumi CLI if it is not already installed and returns a new LocalPulumiCmd instance.
        /// </summary>
        /// <param name="options">Options to configure the LocalPulumiCmd.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public static async Task<LocalPulumiCmd> Install(LocalPulumiCmdOptions? options = null, CancellationToken cancellationToken = default)
        {
            var _assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (_assemblyVersion == null)
            {
                throw new Exception("Failed to get assembly version.");
            }
            var assemblyVersion = new SemVersion(_assemblyVersion.Major, _assemblyVersion.Minor, _assemblyVersion.Build);
            var version = options?.Version ?? assemblyVersion;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var optionsWithDefaults = new LocalPulumiCmdOptions
            {
                Version = version,
                Root = options?.Root ?? Path.Combine(home, ".pulumi", "versions", version.ToString())
            };

            try
            {
                return await CreateAsync(optionsWithDefaults, cancellationToken);
            }
            catch (Exception)
            {
                // Ignore
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await InstallWindowsAsync(optionsWithDefaults.Version, optionsWithDefaults.Root, cancellationToken);
            }
            else
            {
                await InstallPosixAsync(optionsWithDefaults.Version, optionsWithDefaults.Root, cancellationToken);
            }

            return await CreateAsync(optionsWithDefaults, cancellationToken);
        }

        private static async Task InstallWindowsAsync(SemVersion version, string root, CancellationToken cancellationToken)
        {
            var scriptPath = await DownloadToTmpFileAsync("https://get.pulumi.com/install.sh", "install-*.ps1", cancellationToken);
            // TODO:
            throw new NotImplementedException();
        }

        private static async Task InstallPosixAsync(SemVersion version, string root, CancellationToken cancellationToken)
        {
            var scriptPath = await DownloadToTmpFileAsync("https://get.pulumi.com/install.sh", "install-*.sh", cancellationToken);
            try
            {
                var args = new string[] { "--no-edit-path", "--install-root", root, "--version", version.ToString() };
                var result = await Cli.Wrap(scriptPath).WithArguments(args, escape: true)
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync(cancellationToken);
                if (result.ExitCode != 0)
                {
                    throw new Exception($"Failed to install Pulumi ${version} in ${root}: ${result.StandardError}");
                }
            }
            finally
            {
                File.Delete(scriptPath);
            }
        }

        private static async Task<string> DownloadToTmpFileAsync(string url, string extension, CancellationToken cancellationToken)
        {
            var response = await new HttpClient().GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to download {url}");
            }
            var scriptData = await response.Content.ReadAsByteArrayAsync();
            string tempFile = Path.GetTempFileName();
            string tempFileWithExtension = Path.ChangeExtension(tempFile, extension);
            File.Move(tempFile, tempFileWithExtension);
            try
            {

                await File.WriteAllBytesAsync(tempFileWithExtension, scriptData);

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // TODO: In .net7 there is File.SetUnixFileMode https://learn.microsoft.com/en-us/dotnet/api/system.io.file.setunixfilemode?view=net-7.0
                    var args = new string[] { "u+x", tempFileWithExtension };
                    var result = await Cli.Wrap("chmod")
                        .WithArguments(args, escape: true)
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteAsync(cancellationToken);
                    if (result.ExitCode != 0)
                    {
                        throw new Exception($"Failed to chmod u+x {tempFileWithExtension}");
                    }
                }

                return tempFileWithExtension;
            }
            catch (Exception)
            {
                File.Delete(tempFileWithExtension);
                throw;
            }
        }

        internal static SemVersion? ParseAndValidatePulumiVersion(SemVersion minVersion, string currentVersion, bool optOut)
        {
            if (!SemVersion.TryParse(currentVersion, SemVersionStyles.Any, out SemVersion? version))
            {
                version = null;
            }
            if (optOut)
            {
                return version;
            }
            if (version == null)
            {
                throw new InvalidOperationException("Failed to get Pulumi version. This is probably a pulumi error. You can override by version checking by setting {SkipVersionCheckVar}=true.");
            }
            if (minVersion.Major < version.Major)
            {
                throw new InvalidOperationException($"Major version mismatch. You are using Pulumi CLI version {version} with Automation SDK v{minVersion.Major}. Please update the SDK.");
            }
            if (minVersion > version)
            {
                throw new InvalidOperationException($"Minimum version requirement failed. The minimum CLI version requirement is {minVersion}, your current CLI version is {version}. Please update the Pulumi CLI.");
            }
            return version;
        }

        public async Task<CommandResult> RunAsync(
            IList<string> args,
            string workingDir,
            IDictionary<string, string?> additionalEnv,
            Action<string>? onStandardOutput = null,
            Action<string>? onStandardError = null,
            Action<EngineEvent>? onEngineEvent = null,
            CancellationToken cancellationToken = default)
        {
            if (onEngineEvent != null)
            {
                var commandName = SanitizeCommandName(args.FirstOrDefault());
                using var eventLogFile = new EventLogFile(commandName);
                using var eventLogWatcher = new EventLogWatcher(eventLogFile.FilePath, onEngineEvent, cancellationToken);
                try
                {
                    return await RunAsyncInner(args, workingDir, additionalEnv, onStandardOutput, onStandardError, eventLogFile, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await eventLogWatcher.Stop().ConfigureAwait(false);
                }
            }
            return await RunAsyncInner(args, workingDir, additionalEnv, onStandardOutput, onStandardError, eventLogFile: null, cancellationToken).ConfigureAwait(false);
        }

        private async Task<CommandResult> RunAsyncInner(
            IList<string> args,
            string workingDir,
            IDictionary<string, string?> additionalEnv,
            Action<string>? onStandardOutput = null,
            Action<string>? onStandardError = null,
            EventLogFile? eventLogFile = null,
            CancellationToken cancellationToken = default)
        {
            var stdOutBuffer = new StringBuilder();
            var stdOutPipe = PipeTarget.ToStringBuilder(stdOutBuffer);
            if (onStandardOutput != null)
            {
                stdOutPipe = PipeTarget.Merge(stdOutPipe, PipeTarget.ToDelegate(onStandardOutput));
            }

            var stdErrBuffer = new StringBuilder();
            var stdErrPipe = PipeTarget.ToStringBuilder(stdErrBuffer);
            if (onStandardError != null)
            {
                stdErrPipe = PipeTarget.Merge(stdErrPipe, PipeTarget.ToDelegate(onStandardError));
            }

            var pulumiCmd = Cli.Wrap("pulumi")
                .WithArguments(PulumiArgs(args, eventLogFile), escape: true)
                .WithWorkingDirectory(workingDir)
                .WithEnvironmentVariables(PulumiEnvironment(additionalEnv, _command, debugCommands: eventLogFile != null))
                .WithStandardOutputPipe(stdOutPipe)
                .WithStandardErrorPipe(stdErrPipe)
                .WithValidation(CommandResultValidation.None); // we check non-0 exit code ourselves

            var pulumiCmdResult = await pulumiCmd.ExecuteAsync(cancellationToken);

            var result = new CommandResult(
                pulumiCmdResult.ExitCode,
                standardOutput: stdOutBuffer.ToString(),
                standardError: stdErrBuffer.ToString());

            if (pulumiCmdResult.ExitCode != 0)
            {
                throw CommandException.CreateFromResult(result);
            }

            return result;
        }

        internal static IReadOnlyDictionary<string, string?> PulumiEnvironment(IDictionary<string, string?> additionalEnv, string command, bool debugCommands)
        {
            var env = new Dictionary<string, string?>(additionalEnv);

            if (debugCommands)
            {
                // Required for event log
                // We add it after the provided env vars to ensure it is set to true
                env["PULUMI_DEBUG_COMMANDS"] = "true";
            }

            // Prefix PATH with the directory of the pulumi command being run to ensure we prioritize the bundled plugins from the CLI installation.
            if (Path.IsPathRooted(command))
            {
                env["PATH"] = Path.GetDirectoryName(command) + Path.PathSeparator + env["PATH"];
            }

            return env;
        }

        private static IList<string> PulumiArgs(IList<string> args, EventLogFile? eventLogFile)
        {
            // all commands should be run in non-interactive mode.
            // this causes commands to fail rather than prompting for input (and thus hanging indefinitely)
            if (!args.Contains("--non-interactive"))
            {
                args = args.Concat(new[] { "--non-interactive" }).ToList();
            }

            if (eventLogFile != null)
            {
                args = args.Concat(new[] { "--event-log", eventLogFile.FilePath }).ToList();
            }

            return args;
        }

        private static string SanitizeCommandName(string? firstArgument)
        {
            var alphaNumWord = new Regex(@"^[-A-Za-z0-9_]{1,20}$");
            if (firstArgument == null)
            {
                return "event-log";
            }
            return alphaNumWord.IsMatch(firstArgument) ? firstArgument : "event-log";
        }

        private sealed class EventLogFile : IDisposable
        {
            public string FilePath { get; }

            public EventLogFile(string command)
            {
                var logDir = Path.Combine(Path.GetTempPath(), $"automation-logs-{command}-{Path.GetRandomFileName()}");
                Directory.CreateDirectory(logDir);
                this.FilePath = Path.Combine(logDir, "eventlog.txt");
            }

            public void Dispose()
            {
                var dir = Path.GetDirectoryName(this.FilePath);
                System.Diagnostics.Debug.Assert(dir != null, "FilePath had no directory name");
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch (Exception e)
                {
                    // allow graceful exit if for some reason
                    // we're not able to delete the directory
                    // will rely on OS to clean temp directory
                    // in this case.
                    Trace.TraceWarning("Ignoring exception during cleanup of {0} folder: {1}", dir, e);
                }
            }
        }
    }
}

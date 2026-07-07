// Copyright 2016-2026, Pulumi Corporation

// This is boilerplate shared by every consumer of the generated Automation
// API interface, whether it executes commands for real (Standard.cs) or
// captures them for testing (Testing.cs). It is compiled alongside the
// generated Options.cs and Commands.cs, not by the generator project itself.

using System;
using System.Collections.Generic;

namespace Pulumi.Automation.Interface
{
    /// <summary>
    /// The invocation configuration shared by every command: where to run the
    /// CLI, what environment to run it with, and where to send its output.
    /// Every generated options class derives from this so a single argument
    /// carries both the command's flags and its invocation configuration.
    /// </summary>
    public abstract class BaseOptions
    {
        /// <summary>
        /// The working directory in which to run the CLI. Defaults to the
        /// current process directory when null.
        /// </summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>
        /// Environment variables to set for the CLI process, merged over the
        /// inherited environment.
        /// </summary>
        public IDictionary<string, string?>? EnvironmentVariables { get; set; }

        /// <summary>
        /// A callback invoked with each line the CLI writes to standard output.
        /// </summary>
        public Action<string>? OnStandardOutput { get; set; }

        /// <summary>
        /// A callback invoked with each line the CLI writes to standard error.
        /// </summary>
        public Action<string>? OnStandardError { get; set; }
    }

    /// <summary>
    /// The result of running a CLI command.
    /// </summary>
    public sealed class CommandResult
    {
        /// <summary>
        /// Everything the command wrote to standard output.
        /// </summary>
        public string StandardOutput { get; }

        /// <summary>
        /// Everything the command wrote to standard error.
        /// </summary>
        public string StandardError { get; }

        /// <summary>
        /// The process exit code of the command.
        /// </summary>
        public int ExitCode { get; }

        /// <summary>
        /// Creates a command result.
        /// </summary>
        public CommandResult(string standardOutput, string standardError, int exitCode)
        {
            this.StandardOutput = standardOutput;
            this.StandardError = standardError;
            this.ExitCode = exitCode;
        }
    }
}

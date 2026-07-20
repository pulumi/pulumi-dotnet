// Copyright 2016-2026, Pulumi Corporation

// Hand-written half of the generated Automation API interface, compiled
// alongside the generated Options.cs and Commands.cs in this directory.

using System;
using System.Collections.Generic;

namespace Pulumi.Automation.Commands
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
        public string? WorkDir { get; set; }

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
}

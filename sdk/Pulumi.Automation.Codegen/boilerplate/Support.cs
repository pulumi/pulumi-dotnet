// Copyright 2016-2026, Pulumi Corporation

// This is boilerplate shared by every consumer of the generated Automation
// API interface, whether it executes commands for real (Standard.cs) or
// captures them for testing (Testing.cs). It is compiled alongside the
// generated Options.cs and Commands.cs, not by the generator project itself.
//
// Commands return the SDK's existing Pulumi.Automation.Commands.CommandResult
// rather than a copy of it, so results from the generated API and from the
// hand-written one are the same type.

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

// Copyright 2016-2025, Pulumi Corporation

using System;
using System.Threading.Tasks;

namespace Pulumi.Automation.Events
{
    /// <summary>
    /// Interface for watching engine events, either from a file or via gRPC.
    /// </summary>
    internal interface IEventWatcher : IAsyncDisposable
    {
        /// <summary>
        /// Gets the event log path or address to pass to the Pulumi CLI.
        /// For file-based watching, this is a file path.
        /// For gRPC-based watching, this is a TCP address like "tcp://localhost:12345".
        /// </summary>
        string EventLogPath { get; }

        /// <summary>
        /// Stops watching for events and waits for any pending event processing to complete.
        /// </summary>
        Task StopAsync();
    }
}

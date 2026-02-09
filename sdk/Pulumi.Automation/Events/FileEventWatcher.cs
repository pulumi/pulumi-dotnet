// Copyright 2016-2025, Pulumi Corporation

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Pulumi.Automation.Events
{
    /// <summary>
    /// File-based event watcher that polls a log file for engine events.
    /// </summary>
    internal sealed class FileEventWatcher : IEventWatcher
    {
        private readonly string _logDirectory;
        private readonly EventLogWatcher _watcher;

        public string EventLogPath { get; }

        public FileEventWatcher(string commandName, Action<EngineEvent> onEvent, CancellationToken cancellationToken)
        {
            _logDirectory = Path.Combine(Path.GetTempPath(), $"automation-logs-{commandName}-{Path.GetRandomFileName()}");
            Directory.CreateDirectory(_logDirectory);
            EventLogPath = Path.Combine(_logDirectory, "eventlog.txt");
            _watcher = new EventLogWatcher(EventLogPath, onEvent, cancellationToken);
        }

        public async Task StopAsync()
        {
            await _watcher.Stop().ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            _watcher.Dispose();

            // Clean up the log directory
            try
            {
                if (Directory.Exists(_logDirectory))
                {
                    Directory.Delete(_logDirectory, recursive: true);
                }
            }
            catch (Exception)
            {
                // Allow graceful exit if we can't delete the directory
                // Will rely on OS to clean temp directory
            }

            await Task.CompletedTask;
        }
    }
}

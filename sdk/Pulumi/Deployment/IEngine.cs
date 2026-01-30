// Copyright 2016-2026, Pulumi Corporation

using System.Threading.Tasks;

namespace Pulumi.Experimental
{
    /// <summary>
    /// LogSeverity is the severity level of a log message.  Errors are fatal; all others are informational.
    /// </summary>
    public enum LogSeverity
    {
        /// <summary>
        /// A debug-level message not displayed to end-users (the default).
        /// </summary>
        Debug = 0,
        /// <summary>
        /// An informational message printed to output during resource operations.
        /// </summary>
        Info = 1,
        /// <summary>
        /// A warning to indicate that something went wrong.
        /// </summary>
        Warning = 2,
        /// <summary>
        /// A fatal error indicating that the tool should stop processing subsequent resource operations.
        /// </summary>
        Error = 3,
    }


    /// <summary>
    /// A log message to be sent to the Pulumi engine.
    /// </summary>
    public sealed class LogRequest
    {
        /// <summary>
        /// The logging level of this message.
        /// </summary>
        public LogSeverity Severity { get; }

        /// <summary>
        /// The contents of the logged message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// The (optional) resource urn this log is associated with.
        /// </summary>
        public Urn? Urn { get; }

        /// <summary>
        /// The (optional) stream id that a stream of log messages can be associated with. This allows
        /// clients to not have to buffer a large set of log messages that they all want to be
        /// conceptually connected.  Instead the messages can be sent as chunks (with the same stream id)
        /// and the end display can show the messages as they arrive, while still stitching them together
        /// into one total log message.
        /// </summary>
        /// <remarks>
        /// 0 means do not associate with any stream.
        /// </remarks>
        public int StreamId { get; }

        /// <summary>
        /// Optional value indicating whether this is a status message.
        /// </summary>
        public bool Ephemeral { get; }

        public LogRequest(LogSeverity severity, string message, Urn? urn = null, int streamId = 0, bool ephemeral = false)
        {
            Severity = severity;
            Message = message;
            Urn = urn;
            StreamId = streamId;
            Ephemeral = ephemeral;
        }
    }

    /// <summary>
    /// An interface to the engine host running this plugin.
    /// </summary>
    public interface IEngine
    {
        /// <summary>
        /// Send a log message to the host.
        /// </summary>
        public Task LogAsync(LogRequest message);

        /// <summary>
        /// Checks that the version of the Pulumi CLI satisfies the specified version range.
        /// </summary>
        /// <param name="pulumiVersionRange">
        /// A version range to check against the engine (CLI) version. If the version is not compatible with the
        /// specified range, an error is returned. The supported syntax for ranges is that of
        /// https://pkg.go.dev/github.com/blang/semver#ParseRange. For example ">=3.0.0", or "!3.1.2". Ranges can be
        /// AND-ed together by concatenating with spaces ">=3.5.0 !3.7.7", meaning greater-or-equal to 3.5.0 and not
        /// exactly 3.7.7. Ranges can be OR-ed with the || operator: "&lt;3.4.0 || &gt;3.8.0", meaning less-than 3.4.0
        /// or greater-than 3.8.0.
        /// </param>
        public Task RequirePulumiVersionAsync(string pulumiVersionRange);
    }
}

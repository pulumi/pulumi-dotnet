// Copyright 2016-2025, Pulumi Corporation

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
    }
}

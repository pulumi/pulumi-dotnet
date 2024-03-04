using Pulumirpc;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Grpc.Net.Client;

namespace Pulumi.Experimental.Provider
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
    public sealed class LogMessage
    {
        /// <summary>
        /// The logging level of this message.
        /// </summary>
        public readonly LogSeverity Severity;

        /// <summary>
        /// The contents of the logged message.
        /// </summary>
        public readonly string Message;

        /// <summary>
        /// The (optional) resource urn this log is associated with.
        /// </summary>
        public readonly string? Urn;

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
        public readonly int StreamId;

        /// <summary>
        /// Optional value indicating whether this is a status message.
        /// </summary>
        public readonly bool Ephemeral;

        public LogMessage(LogSeverity severity, string message, string? urn = null, int streamId = 0, bool ephemeral = false)
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
    public interface IHost
    {
        /// <summary>
        /// Send a log message to the host.
        /// </summary>
        public Task LogAsync(LogMessage message);
    }

    internal class GrpcHost : IHost
    {
        // TODO: We've got a few instances of channel sharing code, can they all be combined into one helper class?

        private readonly Engine.EngineClient _engine;
        // Using a static dictionary to keep track of and re-use gRPC channels
        // According to the docs (https://docs.microsoft.com/en-us/aspnet/core/grpc/performance?view=aspnetcore-6.0#reuse-grpc-channels), creating GrpcChannels is expensive so we keep track of a bunch of them here
        private static readonly ConcurrentDictionary<string, GrpcChannel> _engineChannels = new ConcurrentDictionary<string, GrpcChannel>();
        private static readonly object _channelsLock = new object();
        public GrpcHost(string engineAddress)
        {
            // maxRpcMessageSize raises the gRPC Max Message size from `4194304` (4mb) to `419430400` (400mb)
            const int maxRpcMessageSize = 400 * 1024 * 1024;
            if (_engineChannels.TryGetValue(engineAddress, out var engineChannel))
            {
                // A channel already exists for this address
                this._engine = new Engine.EngineClient(engineChannel);
            }
            else
            {
                lock (_channelsLock)
                {
                    if (_engineChannels.TryGetValue(engineAddress, out var existingChannel))
                    {
                        // A channel already exists for this address
                        this._engine = new Engine.EngineClient(existingChannel);
                    }
                    else
                    {
                        // Inititialize the engine channel once for this address
                        var channel = GrpcChannel.ForAddress(new Uri($"http://{engineAddress}"), new GrpcChannelOptions
                        {
                            MaxReceiveMessageSize = maxRpcMessageSize,
                            MaxSendMessageSize = maxRpcMessageSize,
                            Credentials = Grpc.Core.ChannelCredentials.Insecure,
                        });

                        _engineChannels[engineAddress] = channel;
                        this._engine = new Engine.EngineClient(channel);
                    }
                }
            }
        }

        public async Task LogAsync(LogMessage message)
        {
            var request = new LogRequest();
            request.Message = message.Message;
            request.Ephemeral = message.Ephemeral;
            request.Urn = message.Urn;
            request.Severity = (Pulumirpc.LogSeverity)message.Severity;
            request.StreamId = message.StreamId;
            await this._engine.LogAsync(request);
        }
    }

}

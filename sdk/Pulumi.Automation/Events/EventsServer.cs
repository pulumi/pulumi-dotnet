// Copyright 2016-2025, Pulumi Corporation

using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Pulumi.Automation.Serialization;
using Pulumirpc;

namespace Pulumi.Automation.Events
{
    /// <summary>
    /// gRPC service implementation for receiving engine events over gRPC.
    /// This service receives events streamed from the Pulumi CLI during stack operations.
    /// </summary>
    internal sealed class EventsServer : Pulumirpc.Events.EventsBase
    {
        private readonly LocalSerializer _localSerializer = new LocalSerializer();
        private readonly Action<EngineEvent> _onEvent;

        public EventsServer(Action<EngineEvent> onEvent)
        {
            _onEvent = onEvent;
        }

        public override async Task<Empty> StreamEvents(IAsyncStreamReader<EventRequest> requestStream, ServerCallContext context)
        {
            try
            {
                while (await requestStream.MoveNext(context.CancellationToken).ConfigureAwait(false))
                {
                    var request = requestStream.Current;
                    var eventJson = request.Event;

                    if (!string.IsNullOrWhiteSpace(eventJson) && _localSerializer.IsValidJson(eventJson))
                    {
                        var engineEvent = _localSerializer.DeserializeJson<EngineEvent>(eventJson);
                        _onEvent.Invoke(engineEvent);
                    }
                }
            }
            catch (Exception) when (context.CancellationToken.IsCancellationRequested)
            {
                // Operation was cancelled, this is expected
            }
            catch (Exception)
            {
                // Log or handle other exceptions if needed
                throw;
            }

            return new Empty();
        }
    }
}

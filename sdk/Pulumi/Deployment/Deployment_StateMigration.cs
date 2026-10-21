// Copyright 2026, Pulumi Corporation

using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;

namespace Pulumi
{
    public partial class Deployment
    {
        internal static Task<Pulumirpc.Callback> AllocateStateMigration(Callbacks callbacks, StateMigration migration)
        {
            return callbacks.AllocateCallback(async (message, cancellationToken) =>
            {
                var request = Serialization.Protobuf.Parse<Pulumirpc.StateMigrationRequest>(message);
                var oldState = JsonNode.Parse(request.OldState.ToStringUtf8()) as JsonArray
                    ?? throw new InvalidOperationException("State migration old state must be a JSON array.");
                var previous = StateMigrationContext.Urn.Value;
                StateMigrationContext.Urn.Value = request.Urn;
                try
                {
                    var result = await migration(new StateMigrationArgs(request.Urn, oldState), cancellationToken).ConfigureAwait(false);
                    if (result == null)
                    {
                        return new Pulumirpc.StateMigrationResponse();
                    }

                    var response = new Pulumirpc.StateMigrationResponse
                    {
                        NewState = ByteString.CopyFromUtf8(result.NewState.ToJsonString()),
                    };
                    foreach (var (oldUrn, newUrn) in result.Successors)
                    {
                        response.Successors.Add(oldUrn, newUrn);
                    }
                    return response;
                }
                finally
                {
                    StateMigrationContext.Urn.Value = previous;
                }
            });
        }
    }

    internal static class StateMigrationContext
    {
        // The monitor waits for the callback, so re-entering it could deadlock. AsyncLocal keeps
        // the guard active across awaits without blocking unrelated work in the deployment.
        internal static readonly AsyncLocal<string?> Urn = new AsyncLocal<string?>();

        internal static void EnsureNotActive(string operation)
        {
            if (Urn.Value is string urn)
            {
                throw new InvalidOperationException(
                    $"Pulumi runtime operation '{operation}' is not allowed inside the state migration callback for '{urn}'. " +
                    "State migration callbacks must only transform the supplied state.");
            }
        }
    }
}

// Copyright 2025, Pulumi Corporation

using System;
using System.Threading.Tasks;

namespace Pulumi
{
    public partial class Deployment
    {
        async Task IDeploymentInternal.SignalAndWaitForShutdownAsync()
        {
            lock (_signalLock)
            {
                if (_hasSignaled)
                {
                    Log.Debug("SignalAndWaitForShutdown: already waiting for shutdown signal from the engine");
                    return;
                }

                _hasSignaled = true;
            }

            Log.Debug("SignalAndWaitForShutdown: waiting for shutdown signal from the engine");
            await Monitor.SignalAndWaitForShutdownAsync().ConfigureAwait(false);
            Log.Debug("SignalAndWaitForShutdown: shutdown signal received successfully");
        }
    }
}

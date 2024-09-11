using System;
using System.Diagnostics;
using System.Threading;

namespace Pulumi.IntegrationTests.Utils;

public static class DebuggerUtils
{
    public static void WaitForDebugger()
    {
        bool.TryParse(Environment.GetEnvironmentVariable("PULUMI_DEBUG"), out var debug);

        if (debug && Pulumi.Deployment.Instance.IsDryRun)
        {
            while (!Debugger.IsAttached)
            {
                Thread.Sleep(100);
            }
        }
    }
}

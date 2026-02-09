// Copyright 2025, Pulumi Corporation.  All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Pulumi;

class ResourceHooksStack : Stack
{
    public ResourceHooksStack()
    {
        var beforeDelete = new ResourceHook("beforeDelete", async (args, cancellationToken) =>
        {
            Console.WriteLine($"BeforeDelete: value was {args.OldOutputs?["value"]}");
        });

        var afterDelete = new ResourceHook("afterDelete", async (args, cancellationToken) =>
        {
            Console.WriteLine($"AfterDelete: value was {args.OldOutputs?["value"]}");
        });
    }
}

class Program
{
    static Task<int> Main(string[] args) => Deployment.RunAsync<ResourceHooksStack>();
}

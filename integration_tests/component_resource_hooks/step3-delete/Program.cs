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
            Console.WriteLine($"BeforeDelete was called");
        });

        var afterDelete = new ResourceHook("afterDelete", async (args, cancellationToken) =>
        {
            Console.WriteLine($"AfterDelete was called");
        });
    }
}

class Program
{
    static Task<int> Main(string[] args) => Deployment.RunAsync<ResourceHooksStack>();
}

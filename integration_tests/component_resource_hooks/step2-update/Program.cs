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

        var c = new Component("component", new ComponentArgs
        {
            Echo = "step2",
        }, new ComponentResourceOptions
        {
            Hooks = {
                BeforeCreate =
                {
                    new("beforeCreate", async (args, cancellationToken) => {
                        Console.WriteLine($"BeforeCreate was called");
                    }),
                },
                AfterCreate =
                {
                    new("afterCreate", async (args, cancellationToken) => {
                        Console.WriteLine($"AfterCreate was called");
                    }),
                },
                BeforeUpdate =
                {
                    new("beforeUpdate", async (args, cancellationToken) => {
                        Console.WriteLine($"BeforeUpdate was called");
                    }),
                },
                AfterUpdate =
                {
                    new("afterUpdate", async (args, cancellationToken) => {
                        Console.WriteLine($"AfterUpdate was called");
                    }),
                },
                BeforeDelete = { beforeDelete },
                AfterDelete = { afterDelete },
            },
        });
    }
}

class Program
{
    static Task<int> Main(string[] args) => Deployment.RunAsync<ResourceHooksStack>();
}

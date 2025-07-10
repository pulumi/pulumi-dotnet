// Copyright 2025, Pulumi Corporation.  All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Pulumi;

class ResourceHooksStack : Stack
{
    public ResourceHooksStack()
    {
        var u = new Updatable("updatable", new UpdatableArgs
        {
            Value = "step4",
        }, new CustomResourceOptions
        {
            Hooks = {
                BeforeCreate =
                {
                    new("beforeCreate", async (args, cancellationToken) => {
                        throw new Exception("BeforeCreate hook failed");
                    }),
                },
                AfterCreate =
                {
                    new("afterCreate", async (args, cancellationToken) => {
                        Console.WriteLine($"AfterCreate: value is {args.NewOutputs?["value"]}");
                    }),
                },
            },
        });
    }
}

class Program
{
    static Task<int> Main(string[] args) => Deployment.RunAsync<ResourceHooksStack>();
}

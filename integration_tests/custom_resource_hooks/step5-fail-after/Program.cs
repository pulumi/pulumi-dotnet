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
            Value = "step5",
        }, new CustomResourceOptions
        {
            Hooks = {
                BeforeCreate =
                {
                    new("beforeCreate", async (args, cancellationToken) => {
                        Console.WriteLine($"BeforeCreate: value is {args.NewInputs?["value"]}");
                    }),
                },
                AfterCreate =
                {
                    new("afterCreate", async (args, cancellationToken) => {
                        throw new Exception("AfterCreate hook failed");
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

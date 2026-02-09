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

        var u = new Updatable("updatable", new UpdatableArgs
        {
            Value = "step1",
            Secret = Output.CreateSecret("hello secret"),
        }, new CustomResourceOptions
        {
            Hooks = {
                BeforeCreate =
                {
                    new("beforeCreate", async (args, cancellationToken) => {
                        Console.WriteLine($"BeforeCreate: value is {args.NewInputs?["value"]}");
                        var secret = (Output<object>)args.NewInputs?["secret"]!;
                        if (await Output.IsSecretAsync(secret)) {
                            Console.WriteLine($"BeforeCreate: secret is secret");
                        }
                        secret.Apply(value => { Console.WriteLine($"BeforeCreate: secret is {value}"); return true; });
                    }),
                },
                AfterCreate =
                {
                    new("afterCreate", async (args, cancellationToken) => {
                        Console.WriteLine($"AfterCreate: value is {args.NewOutputs?["value"]}");
                    }),
                },
                BeforeUpdate =
                {
                    new("beforeUpdate", async (args, cancellationToken) => {
                        Console.WriteLine($"BeforeUpdate: value was {args.OldInputs?["value"]}, is {args.NewInputs?["value"]}");
                    }),
                },
                AfterUpdate =
                {
                    new("afterUpdate", async (args, cancellationToken) => {
                        Console.WriteLine($"AfterUpdate: value was {args.OldOutputs?["value"]}, is {args.NewOutputs?["value"]}");
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

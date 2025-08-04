// Copyright 2025, Pulumi Corporation.  All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Pulumi;

class TransformResourceHooksStack : Stack
{
    public TransformResourceHooksStack()
    {
        static Task HookFunction(ResourceHookArgs args, CancellationToken cancellationToken = default)
        {
            var urnParts = args.Urn.Split("::");
            var resourceType = urnParts.Length > 2 ? urnParts[2] : "";
            if (resourceType != "testprovider:index:Random")
            {
                throw new InvalidOperationException($"Expected type 'testprovider:index:Random', got {resourceType}");
            }
            Console.WriteLine($"Hook was called with length = {args.NewInputs?["length"]}");
            return Task.CompletedTask;
        }

        static Task<ResourceTransformResult?> Transform(ResourceTransformArgs args, CancellationToken cancellationToken = default)
        {
            var opts = args.Options;
            opts.Hooks.AfterCreate.Add(new ResourceHook("transform_hook", HookFunction));
            return Task.FromResult<ResourceTransformResult?>(new ResourceTransformResult(args.Args, opts));
        }

        var res = new Random("res", new RandomArgs
        {
            Length = 10,
        }, new CustomResourceOptions
        {
            ResourceTransforms = { Transform }
        });
    }
}

public class Random : CustomResource
{
    [Output("length")]
    public Output<int> Length { get; private set; } = null!;

    [Output("result")]
    public Output<string> Result { get; private set; } = null!;

    public Random(string name, RandomArgs args, CustomResourceOptions? opts = null)
        : base("testprovider:index:Random", name, args, opts)
    {
    }
}

public class RandomArgs : ResourceArgs
{
    [Input("length", required: true)]
    public Input<int> Length { get; set; } = null!;
}

class Program
{
    static Task<int> Main(string[] args) => Deployment.RunAsync<TransformResourceHooksStack>();
}

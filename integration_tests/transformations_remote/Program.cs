// Copyright 2016-2024, Pulumi Corporation.  All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Pulumi;

class MyComponent : ComponentResource
{
    public Random Child { get; }

    public MyComponent(string name, ComponentResourceOptions? options = null)
        : base("my:component:MyComponent", name, options)
    {
        this.Child = new Random($"{name}-child",
            new RandomArgs { Length = 5 },
            new CustomResourceOptions {Parent = this, AdditionalSecretOutputs = {"length"} });
    }
}

class TransformsStack : Stack
{
    public TransformsStack() : base(new StackOptions { XResourceTransforms = {Scenario3} })
    {
        // Scenario #1 - apply a transformation to a CustomResource
        var res1 = new Random("res1", new RandomArgs { Length = 5 }, new CustomResourceOptions
        {
            XResourceTransforms =
            {
                async (args, _) =>
                {
                    var options = CustomResourceOptions.Merge(
                        (CustomResourceOptions)args.Options,
                        new CustomResourceOptions {AdditionalSecretOutputs = {"result"}});
                    return new ResourceTransformResult(args.Args, options);
                }
            }
        });

        // Scenario #2 - apply a transformation to a Component to transform its children
        var res2 = new MyComponent("res2", new ComponentResourceOptions
        {
            XResourceTransforms =
            {
                async (args, _) =>
                {
                    if (args.Type == "testprovider:index:Random")
                    {
                        var resultArgs = args.Args;
                        resultArgs = resultArgs.SetItem("prefix", "newDefault");

                        var resultOpts = CustomResourceOptions.Merge(
                            (CustomResourceOptions)args.Options,
                            new CustomResourceOptions {AdditionalSecretOutputs = {"result"}});

                        return new ResourceTransformResult(resultArgs, resultOpts);
                    }

                    return null;
                }
            }
        });

        // Scenario #3 - apply a transformation to the Stack to transform all resources in the stack.
        var res3 = new Random("res3", new RandomArgs { Length = Output.CreateSecret(5) });

        // Scenario #4 - Transforms are applied in order of decreasing specificity
        // 1. (not in this example) Child transformation
        // 2. First parent transformation
        // 3. Second parent transformation
        // 4. Stack transformation
        var res4 = new MyComponent("res4", new ComponentResourceOptions
        {
            XResourceTransforms = { (args, _) => scenario4(args, "default1"), (args, _) => scenario4(args, "default2") }
        });

        async Task<ResourceTransformResult?> scenario4(ResourceTransformArgs args, string v)
        {
            if (args.Type == "testprovider:index:Random")
            {
                var resultArgs = args.Args;
                resultArgs = resultArgs.SetItem("prefix", v);
                return new ResourceTransformResult(resultArgs, args.Options);
            }

            return null;
        }

        // Scenario #5 - mutate the properties of a resource
        var res5 = new Random("res5", new RandomArgs { Length = 10 }, new CustomResourceOptions
        {
            XResourceTransforms =
            {
                async (args, _) =>
                {
                    if (args.Type == "testprovider:index:Random")
                    {
                        var resultArgs = args.Args;
                        var length = (double)resultArgs["length"] * 2;
                        resultArgs = resultArgs.SetItem("length", length);
                        return new ResourceTransformResult(resultArgs, args.Options);
                    }

                    return null;
                }
            }
        });
    }

    // Scenario #3 - apply a transformation to the Stack to transform all (future) resources in the stack
    private static async Task<ResourceTransformResult?> Scenario3(ResourceTransformArgs args, CancellationToken ct)
    {
        if (args.Type == "testprovider:index:Random")
        {
            var resultArgs = args.Args;
            resultArgs = resultArgs.SetItem("prefix", "stackDefault");

            var resultOpts = CustomResourceOptions.Merge(
                (CustomResourceOptions)args.Options,
                new CustomResourceOptions {AdditionalSecretOutputs = {"result"}});

            return new ResourceTransformResult(resultArgs, resultOpts);
        }

        return null;
    }
}

class Program
{
    static Task<int> Main(string[] args) => Deployment.RunAsync<TransformsStack>();
}

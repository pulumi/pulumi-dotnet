// Copyright 2026, Pulumi Corporation.  All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Pulumi;

class ErrorHooksStack : Stack
{
    public ErrorHooksStack()
    {
        var onError = new ErrorHook("onError", (args, cancellationToken) =>
        {
            Log.Info($"onError was called for {args.Name} ({args.FailedOperation})");
            return Task.FromResult(true);
        });

        var res = new FlakyCreate("res", new CustomResourceOptions
        {
            Hooks = new ResourceHookBinding
            {
                OnError = { onError },
            },
        });
    }
}

// FlakyCreate is a custom resource that uses testprovider:index:FlakyCreate (fails first create, succeeds on retry).
class FlakyCreate : CustomResource
{
    public FlakyCreate(string name, CustomResourceOptions? opts = null)
        : base("testprovider:index:FlakyCreate", name, ResourceArgs.Empty, opts)
    {
    }
}

class Program
{
    static Task<int> Main(string[] args) => Deployment.RunAsync<ErrorHooksStack>();
}

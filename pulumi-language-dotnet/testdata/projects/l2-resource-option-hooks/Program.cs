using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var hookTestFile = config.Require("hookTestFile");
    var hookPreviewFile = config.Require("hookPreviewFile");
    var createHook = new ResourceHook("createHook", (args, cancellationToken) =>
    {
        var process = Process.Start("touch", new[] { hookTestFile });
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new Exception($"Hook command exited with code {process.ExitCode}.");
        }
        return Task.CompletedTask;
    });
    var previewHook = new ResourceHook("previewHook", (args, cancellationToken) =>
    {
        var process = Process.Start("touch", new[] { $"{hookPreviewFile}_{args.Name}" });
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new Exception($"Hook command exited with code {process.ExitCode}.");
        }
        return Task.CompletedTask;
    }, new ResourceHookOptions
    {
        OnDryRun = true,
    });
    var res = new Simple.Resource("res", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Hooks = new ResourceHookBinding
        {
            BeforeCreate = { createHook, previewHook },
        },
    });

});


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var failingHook = new ResourceHook("failingHook", (args, cancellationToken) =>
    {
        var process = Process.Start("false");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new Exception($"Hook command exited with code {process.ExitCode}.");
        }
        return Task.CompletedTask;
    });
    var res = new Simple.Resource("res", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Hooks = new ResourceHookBinding
        {
            AfterCreate = { failingHook },
        },
    });

});


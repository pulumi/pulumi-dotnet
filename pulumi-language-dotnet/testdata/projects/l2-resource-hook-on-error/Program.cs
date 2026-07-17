using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Pulumi;
using Flaky = Pulumi.Flaky;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var hookTestFile = config.Require("hookTestFile");
    var retryHook = new ErrorHook("retryHook", (args, cancellationToken) =>
    {
        try
        {
            var process = Process.Start("touch", new[] { hookTestFile });
            process.WaitForExit();
            return Task.FromResult(process.ExitCode == 0);
        }
        catch
        {
            return Task.FromResult(false);
        }
    });
    var res = new Flaky.FlakyCreate("res", new()
    {
    }, new CustomResourceOptions
    {
        Hooks = new ResourceHookBinding
        {
            OnError = { retryHook },
        },
    });

});


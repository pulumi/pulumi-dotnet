using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Fail_on_create = Pulumi.Fail_on_create;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var failing = new Fail_on_create.Resource("failing", new()
    {
        Value = false,
    });

    var dependent = new Simple.Resource("dependent", new()
    {
        Value = failing.Value,
    });

    var independent = new Simple.Resource("independent", new()
    {
        Value = true,
    });

    return new Dictionary<string, object?>
    {
        ["failingValue"] = failing.Value,
    };
});


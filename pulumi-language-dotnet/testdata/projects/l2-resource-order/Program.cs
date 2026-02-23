using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var res1 = new Simple.Resource("res1", new()
    {
        Value = true,
    });

    var localVar = res1.Value;

    var res2 = new Simple.Resource("res2", new()
    {
        Value = localVar,
    });

    return new Dictionary<string, object?>
    {
        ["out"] = res2.Value,
    };
});


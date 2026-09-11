using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Large = Pulumi.Large;

return await Deployment.RunAsync(() => 
{
    var res = new Large.Map("res", new()
    {
        Value = "leaf",
        Depth = 300,
    });

    return new Dictionary<string, object?>
    {
        ["output"] = res.Value,
    };
});


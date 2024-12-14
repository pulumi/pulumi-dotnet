using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Large = Pulumi.Large;

return await Deployment.RunAsync(() => 
{
    var res = new Large.String("res", new()
    {
        Value = "hello world",
    });

    return new Dictionary<string, object?>
    {
        ["output"] = res.Value,
    };
});


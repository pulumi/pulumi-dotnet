using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Constant = Pulumi.Constant;

return await Deployment.RunAsync(() => 
{
    var first = new Constant.Resource("first", new()
    {
        Kind = "Constant",
        Flag = true,
        Count = 3,
        Ratio = 1.5,
    });

    return new Dictionary<string, object?>
    {
        ["kind"] = first.Kind,
        ["flag"] = first.Flag,
        ["count"] = first.Count,
        ["ratio"] = first.Ratio,
    };
});


using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Constant = Pulumi.Constant;

return await Deployment.RunAsync(() => 
{
    var first = new Constant.Resource("first", new()
    {
        Kind = "Constant",
    });

    return new Dictionary<string, object?>
    {
        ["kind"] = first.Kind,
    };
});


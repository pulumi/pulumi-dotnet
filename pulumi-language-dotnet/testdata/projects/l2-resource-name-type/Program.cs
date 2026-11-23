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

    return new Dictionary<string, object?>
    {
        ["name"] = res1.GetResourceName(),
        ["type"] = res1.GetResourceType(),
    };
});


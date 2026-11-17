using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Exists = Pulumi.Exists;

return await Deployment.RunAsync(() => 
{
    var res = new Exists.Resource("res", new()
    {
        Value = true,
    });

    var existsResult = Deployment.ExistsResource("exists:index:Resource", res.Id);

    var existsWithState = Deployment.ExistsResource("exists:index:Resource", res.Id, new Dictionary<string, object?> 
    {
        { "value", true },
    });

    var missingWithState = Deployment.ExistsResource("exists:index:Resource", res.Id, new Dictionary<string, object?> 
    {
        { "value", false },
    });

    return new Dictionary<string, object?>
    {
        ["existsResult"] = existsResult,
        ["existsWithState"] = existsWithState,
        ["missingWithState"] = missingWithState,
    };
});


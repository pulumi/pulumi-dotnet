using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Read = Pulumi.Read;

return await Deployment.RunAsync(() => 
{
    var failing = Read.Resource.Get("failing", "existing-id", new Read.ResourceState
    {
        Lookup = "fail",
    });

    return new Dictionary<string, object?>
    {
        ["failingValue"] = failing.Value,
        ["failingLookup"] = failing.Lookup,
    };
});


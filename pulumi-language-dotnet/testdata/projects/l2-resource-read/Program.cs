using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Read = Pulumi.Read;

return await Deployment.RunAsync(() => 
{
    var res = Read.Resource.Get("res", "existing-id", new Read.ResourceState
    {
        Lookup = "existing-key",
    });

    return new Dictionary<string, object?>
    {
        ["resourceId"] = res.Id,
        ["resourceUrn"] = res.Urn,
        ["lookup"] = res.Lookup,
        ["value"] = res.Value,
    };
});


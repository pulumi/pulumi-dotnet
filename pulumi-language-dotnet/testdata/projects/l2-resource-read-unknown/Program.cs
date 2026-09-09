using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Read = Pulumi.Read;

return await Deployment.RunAsync(() => 
{
    var src = new Read.Resource("src", new()
    {
        Value = true,
    });

    var res = Read.Resource.Get("res", src.Id, new Read.ResourceState
    {
        Lookup = "existing-key",
    });

    return new Dictionary<string, object?>
    {
        ["resourceUrn"] = res.Urn,
        ["resourceId"] = res.Id,
        ["lookup"] = res.Lookup,
        ["value"] = res.Value,
    };
});


using System.Collections.Generic;
using System.Linq;
using Pulumi;
using ReadIdRewrite = Pulumi.ReadIdRewrite;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var res = ReadIdRewrite.Resource.Get("res", "existing-id", new ReadIdRewrite.ResourceState
    {
        Lookup = "existing-key",
    });

    var src = new Simple.Resource("src", new()
    {
        Value = true,
    });

    var unknownRes = ReadIdRewrite.Resource.Get("unknownRes", src.Id, new ReadIdRewrite.ResourceState
    {
        Lookup = "another-key",
    });

    return new Dictionary<string, object?>
    {
        ["resourceId"] = res.Id,
        ["lookup"] = res.Lookup,
        ["unknownResourceId"] = unknownRes.Id,
        ["unknownLookup"] = unknownRes.Lookup,
    };
});


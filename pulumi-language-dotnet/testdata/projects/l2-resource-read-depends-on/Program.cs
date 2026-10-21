using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Read = Pulumi.Read;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var src = new Simple.Resource("src", new()
    {
        Value = true,
    });

    var res = Read.Resource.Get("res", "existing-id", new Read.ResourceState
    {
        Lookup = "existing-key",
    }, new CustomResourceOptions
    {
        DependsOn =
        {
            src,
        },
    });

});


using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var ignoreChanges = new Simple.Resource("ignoreChanges", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        IgnoreChanges =
        {
            "value",
        },
    });

    var notIgnoreChanges = new Simple.Resource("notIgnoreChanges", new()
    {
        Value = true,
    });

});


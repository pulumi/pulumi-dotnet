using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var withV2 = new Simple.Resource("withV2", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Version = "2.0.0",
    });

    var withV26 = new Simple.Resource("withV26", new()
    {
        Value = false,
    }, new CustomResourceOptions
    {
        Version = "26.0.0",
    });

    var withDefault = new Simple.Resource("withDefault", new()
    {
        Value = true,
    });

});


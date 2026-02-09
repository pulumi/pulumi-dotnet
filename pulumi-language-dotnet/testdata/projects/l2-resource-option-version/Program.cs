using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var withV2 = new Simple.Resource("withV2", new()
    {
        Value = true,
    });

    var withV26 = new Simple.Resource("withV26", new()
    {
        Value = false,
    });

    var withDefault = new Simple.Resource("withDefault", new()
    {
        Value = true,
    });

});


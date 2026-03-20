using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Nestedobject = Pulumi.Nestedobject;

return await Deployment.RunAsync(() => 
{
    var source = new Nestedobject.Container("source", new()
    {
        Inputs = new[]
        {
            "a",
            "b",
        },
    });

    var sink = new Nestedobject.Container("sink", new()
    {
        Inputs = source.Details.Apply(details => details.Select(__item => __item.Value).ToList()),
    });

});


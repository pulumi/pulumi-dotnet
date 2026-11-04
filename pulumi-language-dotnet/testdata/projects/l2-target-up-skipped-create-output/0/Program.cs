using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Nestedobject = Pulumi.Nestedobject;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var target = new Simple.Resource("target", new()
    {
        Value = true,
    });

    var other = new Nestedobject.Container("other", new()
    {
        Inputs = new[]
        {
            "a",
        },
    });

});


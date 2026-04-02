using System.Collections.Generic;
using System.Linq;
using Pulumi;
using PrimitiveDefaults = Pulumi.PrimitiveDefaults;

return await Deployment.RunAsync(() => 
{
    var resExplicit = new PrimitiveDefaults.Resource("resExplicit", new()
    {
        Boolean = true,
        Float = 3.14,
        Integer = 42,
        String = "hello",
    });

    var resDefaulted = new PrimitiveDefaults.Resource("resDefaulted");

});


using System.Collections.Generic;
using System.Linq;
using Pulumi;
using CamelNames = Pulumi.CamelNames;

return await Deployment.RunAsync(() => 
{
    var firstResource = new CamelNames.CoolModule.SomeResource("firstResource", new()
    {
        TheInput = true,
    });

    var secondResource = new CamelNames.CoolModule.SomeResource("secondResource", new()
    {
        TheInput = firstResource.TheOutput,
    });

    var thirdResource = new CamelNames.CoolModule.SomeResource("thirdResource", new()
    {
        TheInput = true,
        ResourceName = "my-cluster",
    });

});


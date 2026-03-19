using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Keywords = Pulumi.Keywords;

return await Deployment.RunAsync(() => 
{
    var firstResource = new Keywords.SomeResource("firstResource", new()
    {
        Builtins = "builtins",
        Property = "property",
    });

    var secondResource = new Keywords.SomeResource("secondResource", new()
    {
        Builtins = firstResource.Builtins,
        Property = firstResource.Property,
    });

});


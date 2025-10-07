using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var hideDiffs = new Simple.Resource("hideDiffs", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        HideDiffs =
        {
            "@value",
        },
    });

    var notHideDiffs = new Simple.Resource("notHideDiffs", new()
    {
        Value = true,
    });

});


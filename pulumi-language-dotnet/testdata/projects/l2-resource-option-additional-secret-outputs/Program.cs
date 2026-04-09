using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var withSecret = new Simple.Resource("withSecret", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        AdditionalSecretOutputs =
        {
            "value",
        },
    });

    var withoutSecret = new Simple.Resource("withoutSecret", new()
    {
        Value = true,
    });

});


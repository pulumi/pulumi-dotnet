using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var noDependsOn = new Simple.Resource("noDependsOn", new()
    {
        Value = true,
    });

    var withDependsOn = new Simple.Resource("withDependsOn", new()
    {
        Value = false,
    }, new CustomResourceOptions
    {
        DependsOn =
        {
            noDependsOn,
        },
    });

});


using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var targetOnly = new Simple.Resource("targetOnly", new()
    {
        Value = true,
    });

    var dep = new Simple.Resource("dep", new()
    {
        Value = true,
    });

    var unrelated = new Simple.Resource("unrelated", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        DependsOn =
        {
            dep,
        },
    });

});


using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var target = new Simple.Resource("target", new()
    {
        Value = true,
    });

    var replaceWith = new Simple.Resource("replaceWith", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        ReplaceWith = new[]
        {
            target,
        },
    });

    var notReplaceWith = new Simple.Resource("notReplaceWith", new()
    {
        Value = true,
    });

});


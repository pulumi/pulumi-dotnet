using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var target1 = new Simple.Resource("target1", new()
    {
        Value = true,
    });

    var target2 = new Simple.Resource("target2", new()
    {
        Value = true,
    });

    var replaceWith = new Simple.Resource("replaceWith", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        ReplaceWith = new[] { target1, target2 },
    });

    var notReplaceWith = new Simple.Resource("notReplaceWith", new()
    {
        Value = true,
    });

});


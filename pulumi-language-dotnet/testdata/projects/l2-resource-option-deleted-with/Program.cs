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

    var deletedWith = new Simple.Resource("deletedWith", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        DeletedWith = target,
    });

    var notDeletedWith = new Simple.Resource("notDeletedWith", new()
    {
        Value = true,
    });

});


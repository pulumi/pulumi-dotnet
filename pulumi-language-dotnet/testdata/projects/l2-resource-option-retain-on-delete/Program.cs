using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var retainOnDelete = new Simple.Resource("retainOnDelete", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        RetainOnDelete = true,
    });

    var notRetainOnDelete = new Simple.Resource("notRetainOnDelete", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        RetainOnDelete = false,
    });

    var defaulted = new Simple.Resource("defaulted", new()
    {
        Value = true,
    });

});


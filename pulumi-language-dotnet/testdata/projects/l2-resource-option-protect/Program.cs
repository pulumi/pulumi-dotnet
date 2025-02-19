using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var @protected = new Simple.Resource("protected", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Protect = true,
    });

    var unprotected = new Simple.Resource("unprotected", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Protect = false,
    });

    var defaulted = new Simple.Resource("defaulted", new()
    {
        Value = true,
    });

});


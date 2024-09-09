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

    var other = new Simple.Resource("other", new()
    {
        Value = true,
    });

});


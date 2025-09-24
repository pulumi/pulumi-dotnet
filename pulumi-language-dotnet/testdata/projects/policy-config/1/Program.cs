using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var res = new Simple.Resource("res", new()
    {
        Value = false,
    });

});


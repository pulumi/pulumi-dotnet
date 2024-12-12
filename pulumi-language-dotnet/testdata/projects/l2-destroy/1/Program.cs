using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var aresource = new Simple.Resource("aresource", new()
    {
        Value = true,
    });

});


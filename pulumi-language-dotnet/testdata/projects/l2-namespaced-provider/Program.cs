using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Namespaced = ANamespace.Namespaced;

return await Deployment.RunAsync(() => 
{
    var res = new Namespaced.Resource("res", new()
    {
        Value = true,
    });

});


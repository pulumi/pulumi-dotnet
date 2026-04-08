using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Alpha = Pulumi.Alpha;

return await Deployment.RunAsync(() => 
{
    var res = new Alpha.Resource("res", new()
    {
        Value = true,
    });

});


using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var prov = new Simple.Provider("prov");

    var res = new Simple.Resource("res", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Provider = prov,
    });

});


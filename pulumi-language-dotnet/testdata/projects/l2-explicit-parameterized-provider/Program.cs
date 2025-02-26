using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Goodbye = Pulumi.Goodbye;

return await Deployment.RunAsync(() => 
{
    var prov = new Goodbye.Provider("prov", new()
    {
        Text = "World",
    });

    // The resource name is based on the parameter value
    var res = new Goodbye.Goodbye("res", new()
    {
    }, new CustomResourceOptions
    {
        Provider = prov,
    });

    return new Dictionary<string, object?>
    {
        ["parameterValue"] = res.ParameterValue,
    };
});


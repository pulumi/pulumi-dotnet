using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var prov = new Simple.Provider("prov", new()
    {
    }, new CustomResourceOptions
    {
        EnvVarMappings = 
        {
            { "MY_VAR", "PROVIDER_VAR" },
            { "OTHER_VAR", "TARGET_VAR" },
        },
    });

});


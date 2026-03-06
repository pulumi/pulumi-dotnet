using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var aNumber = config.RequireSecretDouble("aNumber");
    return new Dictionary<string, object?>
    {
        ["roundtrip"] = aNumber,
        ["theSecretNumber"] = aNumber.Apply(aNumber => aNumber + 1.25),
    };
});


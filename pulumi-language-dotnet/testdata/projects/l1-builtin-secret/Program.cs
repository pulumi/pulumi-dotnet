using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var aSecret = config.RequireSecret("aSecret");
    var notSecret = config.Require("notSecret");
    return new Dictionary<string, object?>
    {
        ["roundtripSecret"] = aSecret,
        ["roundtripNotSecret"] = notSecret,
        ["double"] = Output.CreateSecret(aSecret),
        ["open"] = Output.Unsecret(aSecret),
        ["close"] = Output.CreateSecret(notSecret),
    };
});


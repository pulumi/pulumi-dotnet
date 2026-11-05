using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var plainNumberArray = config.RequireObject<double[]>("plainNumberArray");
    var plainBooleanMap = config.RequireObject<Dictionary<string, bool>>("plainBooleanMap");
    var secretNumberArray = config.RequireSecretObject<double[]>("secretNumberArray");
    var secretBooleanMap = config.RequireSecretObject<Dictionary<string, bool>>("secretBooleanMap");
    var plain = new Components.PrimitiveComponent("plain", new()
    {
        NumberArray = plainNumberArray,
        BooleanMap = plainBooleanMap,
    });

    var secret = new Components.PrimitiveComponent("secret", new()
    {
        NumberArray = secretNumberArray,
        BooleanMap = secretBooleanMap,
    });

});


using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Primitive = Pulumi.Primitive;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var plainNumberArray = config.RequireObject<double[]>("plainNumberArray");
    var plainBooleanMap = config.RequireObject<Dictionary<string, bool>>("plainBooleanMap");
    var secretNumberArray = config.RequireSecretObject<double[]>("secretNumberArray");
    var secretBooleanMap = config.RequireSecretObject<Dictionary<string, bool>>("secretBooleanMap");
    var plain = new Primitive.Resource("plain", new()
    {
        Boolean = true,
        Float = 3.5,
        Integer = 3,
        String = "plain",
        NumberArray = plainNumberArray,
        BooleanMap = plainBooleanMap,
    });

    var secret = new Primitive.Resource("secret", new()
    {
        Boolean = true,
        Float = 3.5,
        Integer = 3,
        String = "secret",
        NumberArray = secretNumberArray,
        BooleanMap = secretBooleanMap,
    });

});


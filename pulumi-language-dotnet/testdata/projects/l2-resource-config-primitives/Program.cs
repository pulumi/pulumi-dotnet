using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Primitive = Pulumi.Primitive;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var plainBool = config.RequireBoolean("plainBool");
    var plainNumber = config.RequireDouble("plainNumber");
    var plainInteger = config.RequireInt32("plainInteger");
    var plainString = config.Require("plainString");
    var secretBool = config.RequireSecretBoolean("secretBool");
    var secretNumber = config.RequireSecretDouble("secretNumber");
    var secretInteger = config.RequireSecretInt32("secretInteger");
    var secretString = config.RequireSecret("secretString");
    var plain = new Primitive.Resource("plain", new()
    {
        Boolean = plainBool,
        Float = plainNumber,
        Integer = plainInteger,
        String = plainString,
        NumberArray = new[]
        {
            -1.0,
            0.0,
            1.0,
        },
        BooleanMap = 
        {
            { "t", true },
            { "f", false },
        },
    });

    var secret = new Primitive.Resource("secret", new()
    {
        Boolean = secretBool,
        Float = secretNumber,
        Integer = secretInteger,
        String = secretString,
        NumberArray = new[]
        {
            -2.0,
            0.0,
            2.0,
        },
        BooleanMap = 
        {
            { "t", true },
            { "f", false },
        },
    });

});


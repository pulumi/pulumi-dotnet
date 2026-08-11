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
            (double)-1,
            (double)0,
            (double)1,
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
            (double)-2,
            (double)0,
            (double)2,
        },
        BooleanMap = 
        {
            { "t", true },
            { "f", false },
        },
    });

});


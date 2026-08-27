using System.Collections.Generic;
using System.Linq;
using Pulumi;

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
    var plain = new Components.PrimitiveComponent("plain", new()
    {
        Boolean = plainBool,
        Float = plainNumber,
        Integer = plainInteger,
        String = plainString,
    });

    var secret = new Components.PrimitiveComponent("secret", new()
    {
        Boolean = secretBool,
        Float = secretNumber,
        Integer = secretInteger,
        String = secretString,
    });

});


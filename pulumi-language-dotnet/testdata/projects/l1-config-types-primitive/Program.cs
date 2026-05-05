using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var aNumber = config.RequireDouble("aNumber");
    var optionalNumber = config.GetDouble("optionalNumber") ?? 41.5;
    var anInt = config.RequireInt32("anInt");
    var optionalInt = config.GetInt32("optionalInt") ?? 1;
    var aString = config.Require("aString");
    var optionalString = config.Get("optionalString") ?? "defaultStringValue";
    var aBool = config.RequireBoolean("aBool");
    var optionalBool = config.GetBoolean("optionalBool") ?? false;
    return new Dictionary<string, object?>
    {
        ["theNumber"] = aNumber + 1.25,
        ["defaultNumber"] = optionalNumber + 1.2,
        ["theInteger"] = anInt + 4,
        ["defaultInteger"] = optionalInt + 2,
        ["theString"] = $"{aString} World",
        ["defaultString"] = optionalString,
        ["theBool"] = !aBool && true,
        ["defaultBool"] = optionalBool,
    };
});


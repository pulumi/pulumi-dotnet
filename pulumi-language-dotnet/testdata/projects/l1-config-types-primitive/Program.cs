using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var aNumber = config.RequireDouble("aNumber");
    var aString = config.Require("aString");
    var aBool = config.RequireBoolean("aBool");
    return new Dictionary<string, object?>
    {
        ["theNumber"] = aNumber + 1.25,
        ["theString"] = $"{aString} World",
        ["theBool"] = !aBool && true,
    };
});


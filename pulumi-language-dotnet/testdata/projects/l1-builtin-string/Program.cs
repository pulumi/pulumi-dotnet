using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var aString = config.Require("aString");
    return new Dictionary<string, object?>
    {
        ["lengthResult"] = new System.Globalization.StringInfo(aString).LengthInTextElements,
        ["splitResult"] = aString.Split("-"),
        ["joinResult"] = string.Join("|", aString.Split("-")),
        ["interpolateResult"] = $"prefix-{aString}",
    };
});


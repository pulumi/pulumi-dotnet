using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var aList = config.RequireObject<string[]>("aList");
    var singleOrNoneList = config.RequireObject<string[]>("singleOrNoneList");
    var aString = config.Require("aString");
    return new Dictionary<string, object?>
    {
        ["elementOutput1"] = aList[1],
        ["elementOutput2"] = aList[2],
        ["joinOutput"] = string.Join("|", aList),
        ["lengthOutput"] = aList.Length,
        ["splitOutput"] = aString.Split("-"),
        ["singleOrNoneOutput"] = new[]
        {
            Enumerable.SingleOrDefault(singleOrNoneList),
        },
    };
});


using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var aString = config.Require("aString");
    var aNumber = config.RequireDouble("aNumber");
    var aList = config.RequireObject<string[]>("aList");
    var aSecret = config.RequireSecret("aSecret");
    // Nested object using config values
    var nestedObject = new Dictionary<string, object?>
    {
        ["anObject"] = new Dictionary<string, object?>
        {
            ["name"] = aString,
            ["items"] = aList,
        },
        ["a_secret"] = aSecret,
    };

    return new Dictionary<string, object?>
    {
        ["stringOutput"] = JsonSerializer.Serialize(aString),
        ["numberOutput"] = JsonSerializer.Serialize(aNumber),
        ["boolOutput"] = JsonSerializer.Serialize(true),
        ["arrayOutput"] = JsonSerializer.Serialize(new[]
        {
            "x",
            "y",
            "z",
        }),
        ["objectOutput"] = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["key"] = "value",
            ["count"] = 1,
        }),
        ["nestedOutput"] = Output.JsonSerialize(Output.Create(nestedObject)),
    };
});


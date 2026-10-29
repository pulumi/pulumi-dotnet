using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var a = config.RequireDouble("a");
    var b = config.RequireDouble("b");
    var c = config.RequireInt32("c");
    var d = config.RequireInt32("d");
    return new Dictionary<string, object?>
    {
        ["maxResult"] = new[] { a, b }.Max(),
        ["minResult"] = new[] { a, b }.Min(),
        ["intMaxResult"] = new[] { c, d }.Max(),
        ["intMinResult"] = new[] { c, d }.Min(),
    };
});


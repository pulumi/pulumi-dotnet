using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var names = config.GetObject<string[]>("names") ?? new[]
    {
        null,
        "hello",
        null,
    };
    return new Dictionary<string, object?>
    {
        ["namesLength"] = names.Length,
    };
});


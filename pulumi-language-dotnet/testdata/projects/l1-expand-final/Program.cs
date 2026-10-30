using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    return new Dictionary<string, object?>
    {
        ["expandedMax"] = (new[]
        {
            1,
            2,
            3,
        }).Max(),
        ["expandedMaxWithPrefix"] = new[] { 0 }.Concat(new[]
        {
            1,
            2,
            3,
        }).Max(),
    };
});


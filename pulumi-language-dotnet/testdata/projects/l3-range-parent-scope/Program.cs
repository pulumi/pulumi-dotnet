using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Nestedobject = Pulumi.Nestedobject;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var prefix = config.Require("prefix");
    var item = new List<Nestedobject.Target>();
    for (var rangeIndex = 0; rangeIndex < 2; rangeIndex++)
    {
        var range = new { Value = rangeIndex };
        item.Add(new Nestedobject.Target($"item-{range.Value}", new()
        {
            Name = $"{prefix}-{range.Value}",
        }));
    }
});


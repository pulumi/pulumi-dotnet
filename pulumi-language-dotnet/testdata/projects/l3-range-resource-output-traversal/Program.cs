using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Nestedobject = Pulumi.Nestedobject;

return await Deployment.RunAsync(() => 
{
    var container = new Nestedobject.Container("container", new()
    {
        Inputs = new[]
        {
            "alpha",
            "bravo",
        },
    });

    var target = new List<Nestedobject.Target>();
    foreach (var range in container.Details.Select((v, k) => new { Key = k, Value = v }))
    {
        target.Add(new Nestedobject.Target($"target-{range.Key}", new()
        {
            Name = range.Value.Value,
        }));
    }
});


using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Nestedobject = Pulumi.Nestedobject;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var numItems = config.RequireInt32("numItems");
    var itemList = config.RequireObject<string[]>("itemList");
    var numResource = new List<Nestedobject.Target>();
    for (var rangeIndex = 0; rangeIndex < numItems; rangeIndex++)
    {
        var range = new { Value = rangeIndex };
        numResource.Add(new Nestedobject.Target($"numResource-{range.Value}", new()
        {
            Name = $"num-{range.Value}",
        }));
    }
    var numTarget = new Nestedobject.Target("numTarget", new()
    {
        Name = numResource[0].Name.Apply(name => $"{name}+"),
    });

    var listResource = new List<Nestedobject.Target>();
    foreach (var range in itemList.Select((v, k) => new { Key = k, Value = v }))
    {
        listResource.Add(new Nestedobject.Target($"listResource-{range.Key}", new()
        {
            Name = $"{range.Key}:{range.Value}",
        }));
    }
    var listTarget = new Nestedobject.Target("listTarget", new()
    {
        Name = listResource[1].Name.Apply(name => $"{name}+"),
    });

    var listDynTarget = new List<Nestedobject.Target>();
    foreach (var range in itemList.Select((v, k) => new { Key = k, Value = v }))
    {
        listDynTarget.Add(new Nestedobject.Target($"listDynTarget-{range.Key}", new()
        {
            Name = listResource[range.Key].Name.Apply(name => $"{name}!"),
        }));
    }
});


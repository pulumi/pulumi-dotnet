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

    var mapContainer = new Nestedobject.MapContainer("mapContainer", new()
    {
        Tags = 
        {
            { "k1", "charlie" },
            { "k2", "delta" },
        },
    });

    // A resource that ranges over a computed list
    var listOutput = new List<Nestedobject.Target>();
    container.Details.Apply(rangeBody =>
    {
        foreach (var range in rangeBody.Select((v, k) => new { Key = k, Value = v }))
        {
            listOutput.Add(new Nestedobject.Target($"listOutput-{range.Key}", new()
            {
                Name = range.Value.Value,
            }));
        }
        return 0;
    });
    // A resource that ranges over a computed map
    var mapOutput = new List<Nestedobject.Target>();
    mapContainer.Tags.Apply(rangeBody =>
    {
        foreach (var range in rangeBody.Select(pair => new { pair.Key, pair.Value }))
        {
            mapOutput.Add(new Nestedobject.Target($"mapOutput-{range.Key}", new()
            {
                Name = $"{range.Key}=>{range.Value}",
            }));
        }
        return 0;
    });
});


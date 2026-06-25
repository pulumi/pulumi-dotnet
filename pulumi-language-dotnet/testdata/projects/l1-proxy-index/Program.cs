using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var anObject = config.RequireObject<AnObject>("anObject");
    var anyObject = config.RequireObject<JsonElement>("anyObject");
    var l = Output.CreateSecret(new[]
    {
        1,
    });

    var m = Output.CreateSecret(new
    {
        Key = true,
    });

    var c = Output.CreateSecret(anObject);

    var o = Output.CreateSecret(new
    {
        Property = "value",
    });

    var a = Output.CreateSecret(anyObject);

    return new Dictionary<string, object?>
    {
        ["l"] = l.Apply(l => l[0]),
        ["m"] = m.Apply(m => m.Key),
        ["c"] = c.Apply(c => c.Property),
        ["o"] = o.Apply(o => o.Property),
        ["a"] = a.Apply(a => a.GetProperty("property")),
    };
});

public class AnObject
{
    [JsonPropertyName("property")]
    public string Property { get; set; }
}


using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var aMap = config.RequireObject<Dictionary<string, int>>("aMap");
    var anObject = config.RequireObject<AnObject>("anObject");
    var anyObject = config.RequireObject<JsonElement>("anyObject");
    var optionalUntypedObject = config.GetObject<JsonElement?>("optionalUntypedObject") ?? JsonSerializer.SerializeToElement(new Dictionary<string, object?>
    {
        ["key"] = "value",
    });
    var optionalList = config.GetObject<string[]>("optionalList");
    var optionalMap = config.GetObject<Dictionary<string, string>>("optionalMap");
    var optionalObject = config.GetObject<OptionalObject>("optionalObject");
    return new Dictionary<string, object?>
    {
        ["theMap"] = new Dictionary<string, object?>
        {
            ["a"] = aMap["a"] + 1,
            ["b"] = aMap["b"] + 1,
        },
        ["theObject"] = anObject.Prop[0],
        ["theThing"] = anyObject.GetProperty("a").GetDouble() + anyObject.GetProperty("b").GetDouble(),
        ["defaultUntypedObject"] = optionalUntypedObject,
        ["optionalList"] = optionalList == null ? "null" : JsonSerializer.Serialize(optionalList),
        ["optionalMap"] = optionalMap == null ? "null" : JsonSerializer.Serialize(optionalMap),
        ["optionalObject"] = optionalObject == null ? "null" : JsonSerializer.Serialize(optionalObject),
    };
});

public class AnObject
{
    [JsonPropertyName("prop")]
    public List<bool> Prop { get; set; }
}

public class OptionalObject
{
    [JsonPropertyName("other")]
    public int Other { get; set; }
    [JsonPropertyName("prop")]
    public string Prop { get; set; }
}


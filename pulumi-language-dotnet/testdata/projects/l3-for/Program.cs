using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var names = config.RequireObject<string[]>("names");
    var tags = config.RequireObject<Dictionary<string, string>>("tags");
    return new Dictionary<string, object?>
    {
        ["greetings"] = names.Select((value, i) => new { Key = i.ToString(), Value = value }).Select(item => {
            var name = item.Value;
            return $"Hello, {name}!";
        }).ToList(),
        ["numbered"] = names.Select((value, i) => new { Key = i.ToString(), Value = value }).Select(item => {
            var i = item.Key;
            var name = item.Value;
            return $"{i}-{name}";
        }).ToList(),
        ["tagList"] = tags.Select(pair => new { pair.Key, pair.Value }).Select(item => {
            var k = item.Key;
            var v = item.Value;
            return $"{k}={v}";
        }).ToList(),
        ["greetingMap"] = names.Select((value, i) => new { Key = i.ToString(), Value = value }).ToDictionary(item => {
            var name = item.Value;
            return name;
        }, item => {
            var name = item.Value;
            return $"Hello, {name}!";
        }),
        ["filteredList"] = names.Select((value, i) => new { Key = i.ToString(), Value = value }).Where(item => {
            var name = item.Value;
            return name != "b";
        }).Select(item => {
            var name = item.Value;
            return name;
        }).ToList(),
        ["filteredMap"] = names.Select((value, i) => new { Key = i.ToString(), Value = value }).Where(item => {
            var name = item.Value;
            return name != "b";
        }).ToDictionary(item => {
            var name = item.Value;
            return name;
        }, item => {
            var name = item.Value;
            return $"Hello, {name}!";
        }),
    };
});


using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var aMap = config.RequireObject<Dictionary<string, int>>("aMap");
    var anObject = config.RequireObject<AnObject>("anObject");
    var anyObject = config.RequireObject<dynamic>("anyObject");
    return new Dictionary<string, object?>
    {
        ["theMap"] = 
        {
            { "a", aMap.A + 1 },
            { "b", aMap.B + 1 },
        },
        ["theObject"] = anObject.Prop[0],
        ["theThing"] = anyObject.A + anyObject.B,
    };
});

public class AnObject
{
    public List<bool> prop { get; set; }
}


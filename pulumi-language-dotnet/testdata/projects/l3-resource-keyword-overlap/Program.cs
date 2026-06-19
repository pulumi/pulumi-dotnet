using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var comp = new Components.KeywordComponent("comp", new()
    {
        Input = true,
    });

    return new Dictionary<string, object?>
    {
        ["result"] = comp.Result,
    };
});


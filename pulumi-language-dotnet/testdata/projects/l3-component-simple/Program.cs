using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var someComponent = new Components.MyComponent("someComponent", new()
    {
        Input = true,
    });

    return new Dictionary<string, object?>
    {
        ["result"] = someComponent.Output,
    };
});


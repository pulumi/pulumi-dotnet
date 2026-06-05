using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var input = new Simple.Resource("input", new()
    {
        Value = true,
    });

    var someComponent = new Components.MyComponent("someComponent", new()
    {
        Input = input.Value,
    });

    return new Dictionary<string, object?>
    {
        ["result"] = someComponent.Output,
    };
});


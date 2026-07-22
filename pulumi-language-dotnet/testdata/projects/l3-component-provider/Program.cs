using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var myComponent = new Components.ProviderComponent("myComponent", new()
    {
        Text = "hello",
    });

    return new Dictionary<string, object?>
    {
        ["result"] = myComponent.Result,
    };
});


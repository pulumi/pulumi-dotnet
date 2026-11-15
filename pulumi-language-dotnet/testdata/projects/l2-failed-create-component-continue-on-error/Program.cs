using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Failingcomponent = Pulumi.Failingcomponent;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var comp = new Failingcomponent.Component("comp");

    var dependent = new Simple.Resource("dependent", new()
    {
        Value = comp.Value.Apply(@value => @value == "hello"),
    });

    var independent = new Simple.Resource("independent", new()
    {
        Value = true,
    });

    return new Dictionary<string, object?>
    {
        ["componentValue"] = comp.Value,
    };
});


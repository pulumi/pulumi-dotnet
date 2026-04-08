using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Component = Pulumi.Component;

return await Deployment.RunAsync(() => 
{
    var component1 = new Component.ComponentCallable("component1", new()
    {
        Value = "bar",
    });

    return new Dictionary<string, object?>
    {
        ["from_identity"] = component1.Identity().Apply(call => call.Result),
        ["from_prefixed"] = component1.Prefixed(new()
        {
            Prefix = "foo-",
        }).Apply(call => call.Result),
    };
});


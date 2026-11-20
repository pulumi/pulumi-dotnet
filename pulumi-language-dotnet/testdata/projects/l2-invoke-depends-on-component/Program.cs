using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Component = Pulumi.Component;

return await Deployment.RunAsync(() => 
{
    var target = new Component.ComponentCustomRefOutput("target", new()
    {
        Value = "checked",
    });

    var data = Component.Identity.Invoke(new()
    {
        Input = "reachable",
    }, new() {
        DependsOn = new[]
        {
            target,
        },
    });

    return new Dictionary<string, object?>
    {
        ["echoed"] = data.Apply(identityResult => identityResult.Result),
    };
});


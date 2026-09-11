using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Subpackage = Pulumi.Subpackage;

return await Deployment.RunAsync(() => 
{
    return new Dictionary<string, object?>
    {
        ["parameterValue"] = Subpackage.DoHelloWorld.Invoke(new()
        {
            Input = "goodbye",
        }).Apply(invoke => invoke.Output),
    };
});


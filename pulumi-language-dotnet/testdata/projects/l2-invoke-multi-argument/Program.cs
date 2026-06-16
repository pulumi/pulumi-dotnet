using System.Collections.Generic;
using System.Linq;
using Pulumi;
using MultiArgumentInvoke = Pulumi.MultiArgumentInvoke;

return await Deployment.RunAsync(() => 
{
    return new Dictionary<string, object?>
    {
        ["both"] = MultiArgumentInvoke.MultiArgumentInvoke.Invoke("hello", "world").Apply(invoke => invoke.Result),
        ["onlyRequired"] = MultiArgumentInvoke.MultiArgumentInvoke.Invoke("hello").Apply(invoke => invoke.Result),
    };
});


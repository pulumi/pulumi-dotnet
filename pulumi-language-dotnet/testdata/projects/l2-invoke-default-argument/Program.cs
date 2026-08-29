using System.Collections.Generic;
using System.Linq;
using Pulumi;
using SimpleInvoke = Pulumi.SimpleInvoke;

return await Deployment.RunAsync(() => 
{
    return new Dictionary<string, object?>
    {
        ["result"] = SimpleInvoke.InvokeWithDefault.Invoke().Apply(invoke => invoke.Result),
        ["explicitResult"] = SimpleInvoke.InvokeWithDefault.Invoke(new()
        {
            Value = "explicit",
        }).Apply(invoke => invoke.Result),
    };
});


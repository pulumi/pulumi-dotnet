using System.Collections.Generic;
using System.Linq;
using Pulumi;
using SimpleInvoke = Pulumi.SimpleInvoke;

return await Deployment.RunAsync(() => 
{
    return new Dictionary<string, object?>
    {
        ["hello"] = SimpleInvoke.MyInvoke.Invoke(new()
        {
            Value = "hello",
        }).Apply(invoke => invoke.Result),
        ["goodbye"] = SimpleInvoke.MyInvoke.Invoke(new()
        {
            Value = "goodbye",
        }).Apply(invoke => invoke.Result),
    };
});


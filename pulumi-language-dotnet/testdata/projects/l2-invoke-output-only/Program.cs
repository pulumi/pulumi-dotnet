using System.Collections.Generic;
using System.Linq;
using Pulumi;
using OutputOnlyInvoke = Pulumi.OutputOnlyInvoke;

return await Deployment.RunAsync(() => 
{
    return new Dictionary<string, object?>
    {
        ["hello"] = OutputOnlyInvoke.MyInvoke.Invoke(new()
        {
            Value = "hello",
        }).Apply(invoke => invoke.Result),
        ["goodbye"] = OutputOnlyInvoke.MyInvoke.Invoke(new()
        {
            Value = "goodbye",
        }).Apply(invoke => invoke.Result),
    };
});


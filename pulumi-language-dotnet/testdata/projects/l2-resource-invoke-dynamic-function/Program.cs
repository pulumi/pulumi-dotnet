using System.Collections.Generic;
using System.Linq;
using Pulumi;
using AnyTypeFunction = Pulumi.AnyTypeFunction;

return await Deployment.RunAsync(() => 
{
    var localValue = "hello";

    return new Dictionary<string, object?>
    {
        ["dynamic"] = AnyTypeFunction.DynListToDyn.Invoke(new()
        {
            Inputs = new[]
            {
                "hello",
                localValue,
                null,
            },
        }).Apply(invoke => invoke.Result),
    };
});


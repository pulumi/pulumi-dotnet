using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;
using SimpleInvoke = Pulumi.SimpleInvoke;

return await Deployment.RunAsync(() => 
{
    var res = new Simple.Resource("res", new()
    {
        Value = true,
    });

    return new Dictionary<string, object?>
    {
        ["inv"] = SimpleInvoke.MyInvoke.Invoke(new()
        {
            Value = "test",
        }).Apply(invoke => invoke.Result),
    };
});


using System.Collections.Generic;
using System.Linq;
using Pulumi;
using SimpleInvoke = Pulumi.SimpleInvoke;

return await Deployment.RunAsync(() => 
{
    var first = new SimpleInvoke.StringResource("first", new()
    {
        Text = "first hello",
    });

    var data = SimpleInvoke.MyInvoke.Invoke(new()
    {
        Value = "hello",
    }, new() {
        DependsOn = new[]
        {
            first,
        },
    });

    var second = new SimpleInvoke.StringResource("second", new()
    {
        Text = data.Apply(myInvokeResult => myInvokeResult.Result),
    });

    return new Dictionary<string, object?>
    {
        ["hello"] = data.Apply(myInvokeResult => myInvokeResult.Result),
    };
});


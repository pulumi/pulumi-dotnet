using System.Collections.Generic;
using System.Linq;
using Pulumi;
using SimpleInvoke = Pulumi.SimpleInvoke;

return await Deployment.RunAsync(() => 
{
    var res = new SimpleInvoke.StringResource("res", new()
    {
        Text = "hello",
    });

    return new Dictionary<string, object?>
    {
        ["outputInput"] = SimpleInvoke.MyInvoke.Invoke(new()
        {
            Value = res.Text,
        }).Apply(invoke => invoke.Result),
        ["unit"] = SimpleInvoke.Unit.Invoke().Apply(invoke => invoke.Result),
    };
});


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
        ["nonSecret"] = SimpleInvoke.SecretInvoke.Invoke(new()
        {
            Value = "hello",
            SecretResponse = false,
        }).Apply(invoke => invoke.Response),
        ["firstSecret"] = SimpleInvoke.SecretInvoke.Invoke(new()
        {
            Value = "hello",
            SecretResponse = res.Value,
        }).Apply(invoke => invoke.Response),
        ["secondSecret"] = SimpleInvoke.SecretInvoke.Invoke(new()
        {
            Value = Output.CreateSecret("goodbye"),
            SecretResponse = false,
        }).Apply(invoke => invoke.Response),
    };
});


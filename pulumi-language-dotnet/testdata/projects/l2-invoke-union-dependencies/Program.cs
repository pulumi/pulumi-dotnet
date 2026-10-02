using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;
using SimpleInvoke = Pulumi.SimpleInvoke;

return await Deployment.RunAsync(() => 
{
    // Baseline for invoke dependency propagation: an invoke that reads properties from two different
    // resources produces a return value whose consumer must depend on the union of both.
    var a = new SimpleInvoke.StringResource("a", new()
    {
        Text = "hello",
    });

    var b = new Simple.Resource("b", new()
    {
        Value = true,
    });

    var data = SimpleInvoke.SecretInvoke.Invoke(new()
    {
        Value = a.Text,
        SecretResponse = b.Value,
    });

    var d = new SimpleInvoke.StringResource("d", new()
    {
        Text = data.Apply(secretInvokeResult => secretInvokeResult.Response),
    });

});


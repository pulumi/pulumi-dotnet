using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;
using SimpleInvoke = Pulumi.SimpleInvoke;

return await Deployment.RunAsync(() => 
{
    var first = new Simple.Resource("first", new()
    {
        Value = false,
    });

    // assert that resource second depends on resource first
    // because it uses .secret from the invoke which depends on first
    var second = new Simple.Resource("second", new()
    {
        Value = SimpleInvoke.SecretInvoke.Invoke(new()
        {
            Value = "hello",
            SecretResponse = first.Value,
        }).Apply(invoke => invoke.Secret),
    });

});


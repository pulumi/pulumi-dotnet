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

    var third = new SimpleInvoke.StringResource("third", new()
    {
        Text = "third",
    });

    // third.text is known during preview, but third does not exist yet. SDKs must
    // infer the dependency on third from the invoke's arguments and skip the
    // invoke while third's ID is unknown: getText fails if it is called before
    // third has been created.
    var data = SimpleInvoke.GetText.Invoke(new()
    {
        Text = third.Text,
    });

    var fourth = new SimpleInvoke.StringResource("fourth", new()
    {
        Text = data.Apply(getTextResult => getTextResult.Result),
    });

});


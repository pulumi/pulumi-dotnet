using System.Collections.Generic;
using System.Linq;
using Pulumi;
using ScalarReturns = Pulumi.ScalarReturns;

return await Deployment.RunAsync(() => 
{
    return new Dictionary<string, object?>
    {
        ["secret"] = ScalarReturns.InvokeSecret.Invoke(new()
        {
            Value = "goodbye",
        }),
        ["array"] = ScalarReturns.InvokeArray.Invoke(new()
        {
            Value = "the word",
        }),
        ["map"] = ScalarReturns.InvokeMap.Invoke(new()
        {
            Value = "hello",
        }),
        ["secretMap"] = ScalarReturns.InvokeMap.Invoke(new()
        {
            Value = "secret",
        }),
    };
});


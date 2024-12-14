using System.Collections.Generic;
using System.Linq;
using Pulumi;
using SimpleInvoke = Pulumi.SimpleInvoke;

return await Deployment.RunAsync(() => 
{
    var explicitProvider = new SimpleInvoke.Provider("explicitProvider");

    var data = SimpleInvoke.MyInvoke.Invoke(new()
    {
        Value = "hello",
    }, new() {
        Provider = explicitProvider,
        Parent = explicitProvider,
        Version = "10.0.0",
        PluginDownloadURL = "https://example.com/github/example",
    });

    return new Dictionary<string, object?>
    {
        ["hello"] = data.Apply(myInvokeResult => myInvokeResult.Result),
    };
});


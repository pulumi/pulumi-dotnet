using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Call = Pulumi.Call;

return await Deployment.RunAsync(() => 
{
    var explicitProv = new Call.Provider("explicitProv", new()
    {
        Value = "explicitProvValue",
    });

    var explicitRes = new Call.Custom("explicitRes", new()
    {
        Value = "explicitValue",
    }, new CustomResourceOptions
    {
        Provider = explicitProv,
    });

    return new Dictionary<string, object?>
    {
        ["explicitProviderValue"] = explicitRes.ProviderValue().Apply(call => call.Result),
        ["explicitProvFromIdentity"] = explicitProv.Identity().Apply(call => call.Result),
        ["explicitProvFromPrefixed"] = explicitProv.Prefixed(new()
        {
            Prefix = "call-prefix-",
        }).Apply(call => call.Result),
    };
});


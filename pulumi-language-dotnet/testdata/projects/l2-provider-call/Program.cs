using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Call = Pulumi.Call;

return await Deployment.RunAsync(() => 
{
    var defaultRes = new Call.Custom("defaultRes", new()
    {
        Value = "defaultValue",
    });

    return new Dictionary<string, object?>
    {
        ["defaultProviderValue"] = defaultRes.ProviderValue().Apply(call => call.Result),
    };
});


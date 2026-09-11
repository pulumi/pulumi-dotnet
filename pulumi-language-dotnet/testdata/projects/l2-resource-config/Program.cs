using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Config_ = Pulumi.Config_;

return await Deployment.RunAsync(() => 
{
    var prov = new Config_.Provider("prov", new()
    {
        Name = "my config",
        PluginDownloadURL = "not the same as the pulumi resource option",
    });

    // Note this isn't _using_ the explicit provider, it's just grabbing a value from it.
    var res = new Config_.Resource("res", new()
    {
        Text = prov.Version,
    });

    return new Dictionary<string, object?>
    {
        ["pluginDownloadURL"] = prov.PluginDownloadURL,
    };
});


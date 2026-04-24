using System.Collections.Generic;
using System.Linq;
using Pulumi;
using ModuleFormat = Pulumi.ModuleFormat;
using Names = Pulumi.Names;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var names = config.GetBoolean("names") ?? true;
    var Names = config.GetBoolean("Names") ?? true;
    var mod = config.Get("mod") ?? "module";
    var Mod = config.Get("Mod") ?? "format";
    var namesResource = new Names.Mod.Res("namesResource", new()
    {
        Value = names,
    });

    var modResource = new ModuleFormat.Mod.Resource("modResource", new()
    {
        Text = $"{mod}-{Mod}",
    });

    return new Dictionary<string, object?>
    {
        ["namesResourceVal"] = namesResource.Value,
        ["modResourceText"] = modResource.Text,
        ["nameVariables"] = names && Names,
        ["modVariables"] = $"{mod}-{Mod}",
    };
});


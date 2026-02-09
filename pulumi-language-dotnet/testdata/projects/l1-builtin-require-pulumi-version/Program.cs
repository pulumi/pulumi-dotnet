using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var version = config.Require("version");
    Deployment.RequirePulumiVersionAsync(version).Wait();
});


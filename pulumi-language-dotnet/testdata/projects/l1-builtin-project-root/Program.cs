using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    return new Dictionary<string, object?>
    {
        ["projectRootOutput"] = Pulumi.Deployment.Instance.ProjectRoot,
    };
});


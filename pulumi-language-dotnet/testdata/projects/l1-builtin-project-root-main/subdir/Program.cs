using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    return new Dictionary<string, object?>
    {
        ["rootDirectoryOutput"] = Pulumi.Deployment.Instance.RootDirectory,
        ["workingDirectoryOutput"] = Directory.GetCurrentDirectory(),
    };
});


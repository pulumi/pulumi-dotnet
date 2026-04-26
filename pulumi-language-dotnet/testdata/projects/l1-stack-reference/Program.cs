using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var @ref = new Pulumi.StackReference("ref", new()
    {
        Name = "organization/other/dev",
    });

    return new Dictionary<string, object?>
    {
        ["plain"] = @ref.GetOutput("plain"),
        ["secret"] = @ref.GetOutput("secret"),
    };
});


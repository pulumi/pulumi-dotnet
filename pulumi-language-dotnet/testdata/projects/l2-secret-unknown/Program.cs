using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Output_ = Pulumi.Output_;

return await Deployment.RunAsync(() => 
{
    var r = new Output_.Resource("r", new()
    {
        Value = 1.0,
    });

    return new Dictionary<string, object?>
    {
        ["wrapped"] = Output.CreateSecret(r.Output),
    };
});


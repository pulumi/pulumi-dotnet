using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Nestedobject = Pulumi.Nestedobject;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var target = new Simple.Resource("target", new()
    {
        Value = true,
    });

    var other = new Nestedobject.Container("other", new()
    {
        Inputs = new[]
        {
            "a",
        },
    });

    var skipped = new Nestedobject.Container("skipped", new()
    {
        Inputs = new[]
        {
            "b",
        },
    });

    return new Dictionary<string, object?>
    {
        ["skippedOutput"] = skipped.Details.Apply(details => $"skipped-{details[0].Key}"),
    };
});


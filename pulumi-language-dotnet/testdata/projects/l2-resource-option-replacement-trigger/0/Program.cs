using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var replacementTrigger = new Simple.Resource("replacementTrigger", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        ReplacementTrigger = "test",
    });

    var unknown = new global::Pulumi.Output.Resource("unknown", new()
    {
        Value = 1,
    });

    var unknownReplacementTrigger = new Simple.Resource("unknownReplacementTrigger", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        ReplacementTrigger = "hellohello",
    });

    var notReplacementTrigger = new Simple.Resource("notReplacementTrigger", new()
    {
        Value = true,
    });

    var secretReplacementTrigger = new Simple.Resource("secretReplacementTrigger", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        ReplacementTrigger = Output.CreateSecret(new[]
        {
            1,
            2,
            3,
        }),
    });

});


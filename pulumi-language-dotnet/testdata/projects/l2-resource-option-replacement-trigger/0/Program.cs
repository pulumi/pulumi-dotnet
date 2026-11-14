using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Output_ = Pulumi.Output_;
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

    var unknown = new Output_.Resource("unknown", new()
    {
        Value = 1.0,
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
        ReplacementTrigger = Output.CreateSecret(new object?[]
        {
            1,
            2,
            3,
        }),
    });

});


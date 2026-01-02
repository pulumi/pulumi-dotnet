using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var replacementTrigger = new Simple.Resource("replacementTrigger", new()
    {
        Value = true,
    });

    var notReplacementTrigger = new Simple.Resource("notReplacementTrigger", new()
    {
        Value = true,
    });

});


using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    // Stage 1: Change properties to trigger replacements
    // Resource with deleteBeforeReplace option - should delete before creating
    var withOption = new Simple.Resource("withOption", new()
    {
        Value = false,
    }, new CustomResourceOptions
    {
        ReplaceOnChanges =
        {
            "value",
        },
        DeleteBeforeReplace = true,
    });

    // Resource without deleteBeforeReplace - should create before deleting (default)
    var withoutOption = new Simple.Resource("withoutOption", new()
    {
        Value = false,
    }, new CustomResourceOptions
    {
        ReplaceOnChanges =
        {
            "value",
        },
    });

});


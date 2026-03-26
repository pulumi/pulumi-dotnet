using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    // Stage 0: Initial resource creation
    // Resource with deleteBeforeReplace option
    var withOption = new Simple.Resource("withOption", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        ReplaceOnChanges =
        {
            "value",
        },
        DeleteBeforeReplace = true,
    });

    // Resource without deleteBeforeReplace (default create-before-delete behavior)
    var withoutOption = new Simple.Resource("withoutOption", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        ReplaceOnChanges =
        {
            "value",
        },
    });

});


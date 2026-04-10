using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var myStash = new Pulumi.Stash("myStash", new()
    {
        Input = "ignored",
    });

    return new Dictionary<string, object?>
    {
        ["stashInput"] = myStash.Input,
        ["stashOutput"] = myStash.Output,
    };
});


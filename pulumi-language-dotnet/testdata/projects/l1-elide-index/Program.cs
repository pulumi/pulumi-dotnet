using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    // Test that "pkg:typ" type tokens are accepted in PCL and are correctly expanded out. We also have an L2 test around
    // this but it's worth checking with the pulumi schema as it would be too easy for codegen to special case it differently.
    var myStash = new Pulumi.Stash("myStash", new()
    {
        Input = "test",
    });

    return new Dictionary<string, object?>
    {
        ["stashInput"] = myStash.Input,
        ["stashOutput"] = myStash.Output,
    };
});


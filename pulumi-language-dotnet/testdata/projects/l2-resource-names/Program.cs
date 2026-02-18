using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Names = Pulumi.Names;

return await Deployment.RunAsync(() => 
{
    var res1 = new Names.ResMap("res1", new()
    {
        Value = true,
    });

    var res2 = new Names.ResArray("res2", new()
    {
        Value = true,
    });

    var res3 = new Names.ResList("res3", new()
    {
        Value = true,
    });

    var res4 = new Names.ResResource("res4", new()
    {
        Value = true,
    });

    var res5 = new Names.Mod.Res("res5", new()
    {
        Value = true,
    });

    var res6 = new Names.Mod.Nested.Res("res6", new()
    {
        Value = true,
    });

});


using System.Collections.Generic;
using System.Linq;
using Pulumi;
using IndexMod = Pulumi.IndexMod;

return await Deployment.RunAsync(() => 
{
    var res1 = new IndexMod.IndexMine.Resource("res1", new()
    {
        Text = IndexMod.IndexMine.ConcatWorld.Invoke(new()
        {
            Value = "hello",
        }).Apply(invoke => invoke.Result),
    });

    var res2 = new IndexMod.IndexMine.Nested.Resource("res2", new()
    {
        Text = IndexMod.IndexMine.Nested.ConcatWorld.Invoke(new()
        {
            Value = "goodbye",
        }).Apply(invoke => invoke.Result),
    });

    return new Dictionary<string, object?>
    {
        ["out1"] = res1.Call(new()
        {
            Input = "x",
        }).Apply(call => call.Output),
        ["out2"] = res2.Call(new()
        {
            Input = "xx",
        }).Apply(call => call.Output),
    };
});


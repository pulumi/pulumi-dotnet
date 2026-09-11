using System.Collections.Generic;
using System.Linq;
using Pulumi;
using ModuleFormat = Pulumi.ModuleFormat;

return await Deployment.RunAsync(() => 
{
    // This tests that PCL allows both fully specified type tokens, and tokens that only specify the module and
    // member name.
    // First use the fully specified token to invoke and create a resource.
    var res1 = new ModuleFormat.Mod.Resource("res1", new()
    {
        Text = ModuleFormat.Mod.ConcatWorld.Invoke(new()
        {
            Value = "hello",
        }).Apply(invoke => invoke.Result),
    });

    // Next use just the module name as defined by the module format
    var res2 = new ModuleFormat.Mod.Resource("res2", new()
    {
        Text = ModuleFormat.Mod.ConcatWorld.Invoke(new()
        {
            Value = "goodbye",
        }).Apply(invoke => invoke.Result),
    });

    // First use the fully specified token to invoke and create a resource.
    var res3 = new ModuleFormat.Mod.Nested.Resource("res3", new()
    {
        Text = ModuleFormat.Mod.Nested.ConcatWorld.Invoke(new()
        {
            Value = "hello",
        }).Apply(invoke => invoke.Result),
    });

    // Next use just the module name as defined by the module format
    var res4 = new ModuleFormat.Mod.Nested.Resource("res4", new()
    {
        Text = ModuleFormat.Mod.Nested.ConcatWorld.Invoke(new()
        {
            Value = "goodbye",
        }).Apply(invoke => invoke.Result),
    });

    // First use the fully specified token to invoke and create a resource in the index module.
    var res5 = new ModuleFormat.Resource("res5", new()
    {
        Text = ModuleFormat.ConcatWorld.Invoke(new()
        {
            Value = "bonjour",
        }).Apply(invoke => invoke.Result),
    });

    // Next use just the module name as defined by the module format
    var res6 = new ModuleFormat.Resource("res6", new()
    {
        Text = ModuleFormat.ConcatWorld.Invoke(new()
        {
            Value = "youkoso",
        }).Apply(invoke => invoke.Result),
    });

    // Next use the short, 2 component, form because this is the index module
    var res7 = new ModuleFormat.Resource("res7", new()
    {
        Text = ModuleFormat.ConcatWorld.Invoke(new()
        {
            Value = "guten tag",
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
        ["out3"] = res3.Call(new()
        {
            Input = "x",
        }).Apply(call => call.Output),
        ["out4"] = res4.Call(new()
        {
            Input = "xx",
        }).Apply(call => call.Output),
        ["out5"] = res5.Call(new()
        {
            Input = "x",
        }).Apply(call => call.Output),
        ["out6"] = res6.Call(new()
        {
            Input = "xx",
        }).Apply(call => call.Output),
        ["out7"] = res7.Call(new()
        {
            Input = "xxx",
        }).Apply(call => call.Output),
    };
});


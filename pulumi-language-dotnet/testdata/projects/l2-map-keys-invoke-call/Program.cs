using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Component = Pulumi.Component;
using SimpleInvoke = Pulumi.SimpleInvoke;

return await Deployment.RunAsync(() => 
{
    var callable = new Component.ComponentCallable("callable", new()
    {
        Value = "unused",
    });

    return new Dictionary<string, object?>
    {
        ["invokeResult"] = SimpleInvoke.EchoMap.Invoke(new()
        {
            StringMap = 
            {
                { "my key", "one" },
                { "my.key", "two" },
                { "my-key", "three" },
                { "my_key", "four" },
                { "MY_KEY", "five" },
                { "myKey", "six" },
                { "__type", "seven" },
                { "__internal", "eight" },
            },
        }).Apply(invoke => invoke.StringMap),
        ["callResult"] = callable.EchoMap(new()
        {
            StringMap = 
            {
                { "my key", "one" },
                { "my.key", "two" },
                { "my-key", "three" },
                { "my_key", "four" },
                { "MY_KEY", "five" },
                { "myKey", "six" },
                { "__type", "seven" },
                { "__internal", "eight" },
            },
        }).Apply(call => call.StringMap),
    };
});


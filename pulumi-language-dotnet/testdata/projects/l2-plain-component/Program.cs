using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Plaincomponent = Pulumi.Plaincomponent;

return await Deployment.RunAsync(() => 
{
    var myComponent = new Plaincomponent.Component("myComponent", new()
    {
        Name = "my-resource",
        Settings = new Plaincomponent.Inputs.SettingsArgs
        {
            Enabled = true,
            Tags = 
            {
                { "env", "test" },
            },
        },
    });

    return new Dictionary<string, object?>
    {
        ["label"] = myComponent.Label,
    };
});


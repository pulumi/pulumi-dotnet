using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Component = Pulumi.Component;

return await Deployment.RunAsync(() => 
{
    var @explicit = new Component.Provider("explicit");

    var list = new Component.ComponentCallable("list", new()
    {
        Value = "value",
    }, new ComponentResourceOptions
    {
        Providers =
        {
            @explicit,
        },
    });

    var map = new Component.ComponentCallable("map", new()
    {
        Value = "value",
    }, new ComponentResourceOptions
    {
        Providers =
        {
            @explicit,
        },
    });

});


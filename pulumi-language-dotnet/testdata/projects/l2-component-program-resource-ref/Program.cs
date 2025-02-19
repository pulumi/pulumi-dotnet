using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Component = Pulumi.Component;

return await Deployment.RunAsync(() => 
{
    var component1 = new Component.ComponentCustomRefOutput("component1", new()
    {
        Value = "foo-bar-baz",
    });

    var custom1 = new Component.Custom("custom1", new()
    {
        Value = component1.Value,
    });

    var custom2 = new Component.Custom("custom2", new()
    {
        Value = component1.Ref.Apply(@ref => @ref.Value),
    });

});


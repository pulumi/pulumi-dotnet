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

    var component2 = new Component.ComponentCustomRefInputOutput("component2", new()
    {
        InputRef = component1.Ref,
    });

    var custom1 = new Component.Custom("custom1", new()
    {
        Value = component2.InputRef.Apply(inputRef => inputRef.Value),
    });

    var custom2 = new Component.Custom("custom2", new()
    {
        Value = component2.OutputRef.Apply(outputRef => outputRef.Value),
    });

});


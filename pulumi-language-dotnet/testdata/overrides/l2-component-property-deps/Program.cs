using System.Collections.Generic;
using System.Linq;
using Pulumi;
using ComponentPropertyDeps = Pulumi.ComponentPropertyDeps;

return await Deployment.RunAsync(() =>
{
    var custom1 = new ComponentPropertyDeps.Custom("custom1", new()
    {
        Value = "hello",
    });

    var custom2 = new ComponentPropertyDeps.Custom("custom2", new()
    {
        Value = "world",
    });

    var component1 = new ComponentPropertyDeps.Component("component1", new()
    {
        Resource = custom1,
        ResourceList = new()
        {
            custom1,
            custom2,
        },
        ResourceMap =
        {
            { "one", custom1 },
            { "two", custom2 },
        },
    });

    return new Dictionary<string, object?>
    {
        ["propertyDepsFromCall"] = component1.Refs(new()
        {
            Resource = custom1,
            ResourceList = new()
            {
                custom1,
                custom2,
            },
            ResourceMap =
            {
                { "one", custom1 },
                { "two", custom2 },
            },
        }).Apply(call => call.Result),
    };
});

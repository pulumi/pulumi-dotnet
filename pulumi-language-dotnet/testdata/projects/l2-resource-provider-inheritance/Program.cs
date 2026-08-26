using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Primitive = Pulumi.Primitive;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var provider = new Simple.Provider("provider");

    var parent1 = new Simple.Resource("parent1", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Provider = provider,
    });

    // This should inherit the explicit provider from parent1
    var child1 = new Simple.Resource("child1", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Parent = parent1,
    });

    var parent2 = new Primitive.Resource("parent2", new()
    {
        Boolean = false,
        Float = 0.0,
        Integer = 0,
        String = "",
        NumberArray = new() {},
        BooleanMap = new() { },
    });

    // This _should not_ inherit the provider from parent2 as it is a default provider.
    var child2 = new Simple.Resource("child2", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Parent = parent2,
    });

    // This _should not_ inherit the provider from parent1 as its from the wrong package.
    var child3 = new Primitive.Resource("child3", new()
    {
        Boolean = false,
        Float = 0.0,
        Integer = 0,
        String = "",
        NumberArray = new() {},
        BooleanMap = new() { },
    }, new CustomResourceOptions
    {
        Parent = parent1,
    });

});


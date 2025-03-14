using System.Collections.Generic;
using System.Linq;
using Pulumi;
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

    var child1 = new Simple.Resource("child1", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Parent = parent1,
    });

    var orphan1 = new Simple.Resource("orphan1", new()
    {
        Value = true,
    });

    var parent2 = new Simple.Resource("parent2", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Protect = true,
    });

    var child2 = new Simple.Resource("child2", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Parent = parent2,
    });

    var child3 = new Simple.Resource("child3", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Parent = parent2,
        Protect = false,
    });

    var orphan2 = new Simple.Resource("orphan2", new()
    {
        Value = true,
    });

});


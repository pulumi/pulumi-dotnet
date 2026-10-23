using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Fail_on_create = Pulumi.Fail_on_create;
using Read = Pulumi.Read;

return await Deployment.RunAsync(() => 
{
    var failing = new Fail_on_create.Resource("failing", new()
    {
        Value = false,
    });

    var res = Read.Resource.Get("res", "existing-id", new Read.ResourceState
    {
        Lookup = "existing-key",
    }, new CustomResourceOptions
    {
        DependsOn =
        {
            failing,
        },
    });

    var res_prop_dep = Read.Resource.Get("res_prop_dep", "existing-id", new Read.ResourceState
    {
        Lookup = failing.Value.Apply(@value => $"existing-key-{@value}"),
    });

    return new Dictionary<string, object?>
    {
        ["readValue"] = res.Value,
        ["readPropDepValue"] = res_prop_dep.Value,
    };
});


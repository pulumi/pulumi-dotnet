using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Fail_on_create = Pulumi.Fail_on_create;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var failing = new Fail_on_create.Resource("failing", new()
    {
        Value = false,
    });

    var dependent = new Simple.Resource("dependent", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        DependsOn =
        {
            failing,
        },
    });

    var dependent_on_output = new Simple.Resource("dependent_on_output", new()
    {
        Value = failing.Value,
    });

    var independent = new Simple.Resource("independent", new()
    {
        Value = true,
    });

    var double_dependency = new Simple.Resource("double_dependency", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        DependsOn =
        {
            independent,
            dependent_on_output,
        },
    });

});


using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var noTimeouts = new Simple.Resource("noTimeouts", new()
    {
        Value = true,
    });

    var createOnly = new Simple.Resource("createOnly", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        CustomTimeouts = new CustomTimeouts
        {
            Create = System.TimeSpan.FromSeconds(300),
        },
    });

    var updateOnly = new Simple.Resource("updateOnly", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        CustomTimeouts = new CustomTimeouts
        {
            Update = System.TimeSpan.FromSeconds(600),
        },
    });

    var deleteOnly = new Simple.Resource("deleteOnly", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        CustomTimeouts = new CustomTimeouts
        {
            Delete = System.TimeSpan.FromSeconds(180),
        },
    });

    var allTimeouts = new Simple.Resource("allTimeouts", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        CustomTimeouts = new CustomTimeouts
        {
            Create = System.TimeSpan.FromSeconds(120),
            Update = System.TimeSpan.FromSeconds(240),
            Delete = System.TimeSpan.FromSeconds(60),
        },
    });

});


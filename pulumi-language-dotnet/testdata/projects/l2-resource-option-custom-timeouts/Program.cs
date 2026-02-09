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
    });

    var updateOnly = new Simple.Resource("updateOnly", new()
    {
        Value = true,
    });

    var deleteOnly = new Simple.Resource("deleteOnly", new()
    {
        Value = true,
    });

    var allTimeouts = new Simple.Resource("allTimeouts", new()
    {
        Value = true,
    });

});


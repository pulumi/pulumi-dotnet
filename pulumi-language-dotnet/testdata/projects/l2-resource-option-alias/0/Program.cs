using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Component = Pulumi.Component;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    // Make a simple resource to use as a parent
    var parent = new Simple.Resource("parent", new()
    {
        Value = true,
    });

    var aliasURN = new Simple.Resource("aliasURN", new()
    {
        Value = true,
    });

    var aliasName = new Simple.Resource("aliasName", new()
    {
        Value = true,
    });

    var aliasNoParent = new Simple.Resource("aliasNoParent", new()
    {
        Value = true,
    });

    var aliasParent = new Simple.Resource("aliasParent", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Parent = aliasURN,
    });

    var aliasType = new Component.Custom("aliasType", new()
    {
        Value = "true",
    });

});


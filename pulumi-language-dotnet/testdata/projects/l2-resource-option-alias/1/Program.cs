using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var parent = new Simple.Resource("parent", new()
    {
        Value = true,
    });

    var aliasURN = new Simple.Resource("aliasURN", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Parent = parent,
        Aliases =
        {
            new Alias { Urn = "urn:pulumi:test::l2-resource-option-alias::simple:index:Resource::aliasURN" },
        },
    });

    var aliasNewName = new Simple.Resource("aliasNewName", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Aliases =
        {
            new Alias
            {
                Name = "aliasName",
            },
        },
    });

    var aliasNoParent = new Simple.Resource("aliasNoParent", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Parent = parent,
        Aliases =
        {
            new Alias
            {
                NoParent = true,
            },
        },
    });

    var aliasParent = new Simple.Resource("aliasParent", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Parent = parent,
        Aliases =
        {
            new Alias
            {
                Parent = aliasURN,
            },
        },
    });

    var aliasType = new Simple.Resource("aliasType", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        Aliases =
        {
            new Alias
            {
                Type = "component:index:Custom",
            },
        },
    });

});


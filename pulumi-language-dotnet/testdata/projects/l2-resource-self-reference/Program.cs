using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Selfref = Pulumi.Selfref;

return await Deployment.RunAsync(() => 
{
    var root = new Selfref.Node("root");

    var child = new Selfref.Node("child", new()
    {
        Parent = root,
        Parents = new[]
        {
            root,
        },
        NamedParents = 
        {
            { "root", root },
        },
        ParentOrName = root,
    });

});


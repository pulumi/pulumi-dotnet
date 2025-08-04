using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Component = Pulumi.Component;
using Namespaced = ANamespace.Namespaced;

return await Deployment.RunAsync(() => 
{
    var componentRes = new Component.ComponentCustomRefOutput("componentRes", new()
    {
        Value = "foo-bar-baz",
    });

    var res = new Namespaced.Resource("res", new()
    {
        Value = true,
        ResourceRef = componentRes.Ref,
    });

});


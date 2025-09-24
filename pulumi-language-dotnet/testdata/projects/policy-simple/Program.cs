using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var res1 = new Simple.Resource("res1", new()
    {
        Value = true,
    });

    var res2 = new Simple.Resource("res2", new()
    {
        Value = res1.Value.Apply(@value => !@value),
    });

});


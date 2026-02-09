using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var withDefaultURL = new Simple.Resource("withDefaultURL", new()
    {
        Value = true,
    });

    var withExplicitDefaultURL = new Simple.Resource("withExplicitDefaultURL", new()
    {
        Value = true,
    });

    var withCustomURL1 = new Simple.Resource("withCustomURL1", new()
    {
        Value = true,
    });

    var withCustomURL2 = new Simple.Resource("withCustomURL2", new()
    {
        Value = false,
    });

});


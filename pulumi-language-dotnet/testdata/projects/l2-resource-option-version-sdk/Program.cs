using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    // Check that withV2 is generated against the v2 SDK and not against the V26 SDK,
    // and that the version resource option is elided.
    var withV2 = new Simple.Resource("withV2", new()
    {
        Value = true,
    });

});


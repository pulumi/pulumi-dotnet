using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Myborrow = Pulumi.Myborrow;

return await Deployment.RunAsync(() => 
{
    var widget = new Myborrow.Widget("widget");

    return new Dictionary<string, object?>
    {
        ["metadataName"] = widget.Metadata.Apply(metadata => metadata.Name),
    };
});


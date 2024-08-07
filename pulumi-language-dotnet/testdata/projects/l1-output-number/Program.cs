using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    return new Dictionary<string, object?>
    {
        ["zero"] = 0,
        ["one"] = 1,
        ["e"] = 2.718,
        ["minInt32"] = -2147483648,
        ["max"] = 1.7976931348623157e+308,
        ["min"] = 5e-324,
    };
});


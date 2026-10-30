using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    return new Dictionary<string, object?>
    {
        ["sumTimes"] = (1 + 2) * 3.0,
        ["minusMinus"] = 10.0 - (4 - 3),
        ["notAnd"] = !(true && false),
        ["negateSum"] = -(1 + 2),
        ["conditionalIndex"] = (true ? new[]
        {
            1,
        } : new[]
        {
            2,
        })[0],
        ["conditionalCondition"] = (true ? false : true) ? "yes" : "no",
    };
});


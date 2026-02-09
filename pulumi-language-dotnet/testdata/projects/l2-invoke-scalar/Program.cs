using System.Collections.Generic;
using System.Linq;
using Pulumi;
using SimpleInvokeWithScalarReturn = Pulumi.SimpleInvokeWithScalarReturn;

return await Deployment.RunAsync(() => 
{
    return new Dictionary<string, object?>
    {
        ["scalar"] = SimpleInvokeWithScalarReturn.MyInvokeScalar.Invoke(new()
        {
            Value = "goodbye",
        }),
    };
});


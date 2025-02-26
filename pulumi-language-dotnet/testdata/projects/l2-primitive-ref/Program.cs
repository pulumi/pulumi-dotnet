using System.Collections.Generic;
using System.Linq;
using Pulumi;
using PrimitiveRef = Pulumi.PrimitiveRef;

return await Deployment.RunAsync(() => 
{
    var res = new PrimitiveRef.Resource("res", new()
    {
        Data = new PrimitiveRef.Inputs.DataArgs
        {
            Boolean = false,
            Float = 2.17,
            Integer = -12,
            String = "Goodbye",
            BoolArray = new[]
            {
                false,
                true,
            },
            StringMap = 
            {
                { "two", "turtle doves" },
                { "three", "french hens" },
            },
        },
    });

});


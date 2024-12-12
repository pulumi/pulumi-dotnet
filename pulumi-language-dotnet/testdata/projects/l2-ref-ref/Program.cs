using System.Collections.Generic;
using System.Linq;
using Pulumi;
using RefRef = Pulumi.RefRef;

return await Deployment.RunAsync(() => 
{
    var res = new RefRef.Resource("res", new()
    {
        Data = new RefRef.Inputs.DataArgs
        {
            InnerData = new RefRef.Inputs.InnerDataArgs
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
            Boolean = true,
            Float = 4.5,
            Integer = 1024,
            String = "Hello",
            BoolArray = new() { },
            StringMap = 
            {
                { "x", "100" },
                { "y", "200" },
            },
        },
    });

});


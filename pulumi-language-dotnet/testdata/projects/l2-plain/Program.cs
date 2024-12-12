using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Plain = Pulumi.Plain;

return await Deployment.RunAsync(() => 
{
    var res = new Plain.Resource("res", new()
    {
        Data = new Plain.Inputs.DataArgs
        {
            InnerData = new Plain.Inputs.InnerDataArgs
            {
                Boolean = false,
                Float = 2.17,
                Integer = -12,
                String = "Goodbye",
                BoolArray = new()
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
            BoolArray = new()
            {
                true,
                false,
            },
            StringMap = 
            {
                { "x", "100" },
                { "y", "200" },
            },
        },
    });

});


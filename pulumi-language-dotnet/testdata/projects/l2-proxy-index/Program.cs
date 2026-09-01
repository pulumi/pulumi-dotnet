using System.Collections.Generic;
using System.Linq;
using Pulumi;
using RefRef = Pulumi.RefRef;

return await Deployment.RunAsync(() => 
{
    // Check we can index into properties of objects returned in outputs, this is similar to ref-ref but 
    // we index into the outputs
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
            BoolArray = new[]
            {
                true,
            },
            StringMap = 
            {
                { "x", "100" },
                { "y", "200" },
            },
            InnerDataList = new[]
            {
                new RefRef.Inputs.InnerDataArgs
                {
                    Boolean = false,
                    Float = 3.14,
                    Integer = 42,
                    String = "Partridge",
                    BoolArray = new[]
                    {
                        true,
                    },
                    StringMap = 
                    {
                        { "one", "in a pear tree" },
                    },
                },
            },
        },
    });

    return new Dictionary<string, object?>
    {
        ["bool"] = res.Data.Apply(data => data.Boolean),
        ["array"] = res.Data.Apply(data => data.BoolArray[0]),
        ["map"] = res.Data.Apply(data => data.StringMap["x"]),
        ["nested"] = res.Data.Apply(data => data.InnerData.StringMap["three"]),
        ["listIndex"] = res.Data.Apply(data => data.InnerDataList[0]?.String),
    };
});


using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Primitive = Pulumi.Primitive;

return await Deployment.RunAsync(() => 
{
    var res = new Primitive.Resource("res", new()
    {
        Boolean = true,
        Float = 3.14,
        Integer = 42,
        String = "hello",
        NumberArray = new[]
        {
            -1.0,
            0.0,
            1.0,
        },
        BooleanMap = 
        {
            { "t", true },
            { "f", false },
        },
    });

});


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
            (double)-1,
            (double)0,
            (double)1,
        },
        BooleanMap = 
        {
            { "t", true },
            { "f", false },
        },
    });

});


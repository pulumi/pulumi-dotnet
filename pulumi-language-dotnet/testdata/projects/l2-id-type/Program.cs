using System;
using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Primitive = Pulumi.Primitive;

return await Deployment.RunAsync(() => 
{
    // Test that the ID type is treated the same as a string type, despite being tracked as a distinct type. This 
    // includes directly passing it to string fields, but also for bool and numeric values being able to cast to it.
    var source1 = new Primitive.Resource("source1", new()
    {
        Boolean = false,
        Float = 1.0,
        Integer = 2,
        String = "1234",
        NumberArray = new[]
        {
            3.0,
        },
        BooleanMap = 
        {
            { "source", false },
        },
    });

    var source2 = new Primitive.Resource("source2", new()
    {
        Boolean = false,
        Float = 1.0,
        Integer = 2,
        String = "true",
        NumberArray = new[]
        {
            3.0,
        },
        BooleanMap = 
        {
            { "source", false },
        },
    });

    var idMap = new Dictionary<string, Output<string>>
    {
        ["source1Token"] = source1.Id,
        ["source2Token"] = source2.Id,
    };

    var sink1 = new Primitive.Resource("sink1", new()
    {
        Boolean = false,
        Float = idMap["source1Token"].Apply(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)),
        Integer = idMap["source1Token"].Apply(x => int.Parse(x, System.Globalization.CultureInfo.InvariantCulture)),
        String = idMap["source1Token"],
        NumberArray = new[]
        {
            idMap["source1Token"].Apply(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)),
        },
        BooleanMap = 
        {
            { "sink", false },
        },
    });

    var sink2 = new Primitive.Resource("sink2", new()
    {
        Boolean = idMap["source2Token"].Apply(x => x == "true"),
        Float = 1.0,
        Integer = 2,
        String = "abc",
        NumberArray = new[]
        {
            3.0,
        },
        BooleanMap = 
        {
            { "sink", idMap["source2Token"].Apply(x => x == "true") },
        },
    });

    return new Dictionary<string, object?>
    {
        ["ids"] = idMap,
        ["base64"] = sink2.Id.Apply(id => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(id))),
    };
});


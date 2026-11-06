using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Union = Pulumi.Union;

return await Deployment.RunAsync(() => 
{
    var stringOrIntegerExample1 = new Union.Example("stringOrIntegerExample1", new()
    {
        StringOrIntegerProperty = 42,
    });

    var stringOrIntegerExample2 = new Union.Example("stringOrIntegerExample2", new()
    {
        StringOrIntegerProperty = "forty two",
    });

    var mapMapUnionExample = new Union.Example("mapMapUnionExample", new()
    {
        MapMapUnionProperty = 
        {
            { "key1", new InputMap<Pulumi.Union<string, System.Collections.Immutable.ImmutableArray<string>>>
            {
                { "key1a", "value1a" },
            } },
        },
    });

    // List<Union<String, Enum>> pattern
    var stringEnumUnionListExample = new Union.Example("stringEnumUnionListExample", new()
    {
        StringEnumUnionListProperty = 
        {
            Union.AccessRights.Listen,
            Union.AccessRights.Send,
            "NotAnEnumValue",
        },
    });

    // Safe enum: literal string matching an enum value
    var safeEnumExample = new Union.Example("safeEnumExample", new()
    {
        TypedEnumProperty = Union.BlobType.Block,
    });

    // Output enum: output from another resource used as enum input
    var enumOutputExample = new Union.EnumOutput("enumOutputExample", new()
    {
        Name = "example",
    });

    var outputEnumExample = new Union.Example("outputEnumExample", new()
    {
        TypedEnumProperty = enumOutputExample.Type,
    });

    return new Dictionary<string, object?>
    {
        ["mapMapUnionOutput"] = mapMapUnionExample.MapMapUnionProperty,
    };
});


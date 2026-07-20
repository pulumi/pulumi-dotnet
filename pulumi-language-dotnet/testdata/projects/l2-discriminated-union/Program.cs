using System.Collections.Generic;
using System.Linq;
using Pulumi;
using DiscriminatedUnion = Pulumi.DiscriminatedUnion;

return await Deployment.RunAsync(() => 
{
    var example1 = new DiscriminatedUnion.Example("example1", new()
    {
        UnionOf = new DiscriminatedUnion.Inputs.VariantOneArgs
        {
            DiscriminantKind = "variant1",
            Field1 = "v1 union",
        },
        ArrayOfUnionOf = 
        {
            new DiscriminatedUnion.Inputs.VariantOneArgs
            {
                DiscriminantKind = "variant1",
                Field1 = "v1 array(union)",
            },
        },
    });

    var example2 = new DiscriminatedUnion.Example("example2", new()
    {
        UnionOf = new DiscriminatedUnion.Inputs.VariantTwoArgs
        {
            DiscriminantKind = "variant2",
            Field2 = "v2 union",
        },
        ArrayOfUnionOf = 
        {
            new DiscriminatedUnion.Inputs.VariantTwoArgs
            {
                DiscriminantKind = "variant2",
                Field2 = "v2 array(union)",
            },
            new DiscriminatedUnion.Inputs.VariantOneArgs
            {
                DiscriminantKind = "variant1",
                Field1 = "v1 array(union)",
            },
        },
    });

});


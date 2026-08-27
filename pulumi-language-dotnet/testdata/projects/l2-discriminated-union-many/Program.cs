using System.Collections.Generic;
using System.Linq;
using Pulumi;
using DiscriminatedUnionMany = Pulumi.DiscriminatedUnionMany;

return await Deployment.RunAsync(() => 
{
    var example1 = new DiscriminatedUnionMany.Example("example1", new()
    {
        UnionOf = new DiscriminatedUnionMany.Inputs.Variant1Args
        {
            DiscriminantKind = "variant1",
            Payload = "p1",
            Extra = "e1",
        },
    });

    var example2 = new DiscriminatedUnionMany.Example("example2", new()
    {
        UnionOf = new DiscriminatedUnionMany.Inputs.Variant2Args
        {
            DiscriminantKind = "variant2",
            Payload = "p2",
            Extra = "e2",
        },
    });

    var example3 = new DiscriminatedUnionMany.Example("example3", new()
    {
        UnionOf = new DiscriminatedUnionMany.Inputs.Variant3Args
        {
            DiscriminantKind = "variant3",
            Payload = "p3",
            Count = 3,
        },
    });

    var example4 = new DiscriminatedUnionMany.Example("example4", new()
    {
        UnionOf = new DiscriminatedUnionMany.Inputs.Variant4Args
        {
            DiscriminantKind = "variant4",
            Payload = "p4",
            Enabled = true,
        },
    });

    var example5 = new DiscriminatedUnionMany.Example("example5", new()
    {
        UnionOf = new DiscriminatedUnionMany.Inputs.Variant5Args
        {
            DiscriminantKind = "variant5",
            Payload = "p5",
            Label = "l5",
        },
    });

    var example6 = new DiscriminatedUnionMany.Example("example6", new()
    {
        UnionOf = new DiscriminatedUnionMany.Inputs.Variant6Args
        {
            DiscriminantKind = "variant6",
            Payload = "p6",
            Code = 6,
        },
    });

    var example7 = new DiscriminatedUnionMany.Example("example7", new()
    {
        UnionOf = new DiscriminatedUnionMany.Inputs.Variant7Args
        {
            DiscriminantKind = "variant7",
            Payload = "p7",
            Message = "m7",
        },
    });

    var example8 = new DiscriminatedUnionMany.Example("example8", new()
    {
        UnionOf = new DiscriminatedUnionMany.Inputs.Variant8Args
        {
            DiscriminantKind = "variant8",
            Payload = "p8",
            Size = 8,
        },
    });

    var example9 = new DiscriminatedUnionMany.Example("example9", new()
    {
        UnionOf = new DiscriminatedUnionMany.Inputs.Variant9Args
        {
            DiscriminantKind = "variant9",
            Payload = "p9",
            Flag = false,
        },
    });

    var example10 = new DiscriminatedUnionMany.Example("example10", new()
    {
        UnionOf = new DiscriminatedUnionMany.Inputs.Variant10Args
        {
            DiscriminantKind = "variant10",
            Payload = "p10",
            Note = "n10",
        },
    });

    // A SubsetExample's unionOf is typed as a 3-variant subset union. We should be
    // able to assign that output to an Example's unionOf, which is typed as the
    // full 10-variant union.
    var subset1 = new DiscriminatedUnionMany.SubsetExample("subset1", new()
    {
        UnionOf = new DiscriminatedUnionMany.Inputs.Variant3Args
        {
            DiscriminantKind = "variant3",
            Payload = "sp",
            Count = 33,
        },
    });

    var example11 = new DiscriminatedUnionMany.Example("example11", new()
    {
        UnionOf = subset1.UnionOf,
    });

});


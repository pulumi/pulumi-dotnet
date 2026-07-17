using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Enum = Pulumi.Enum;

return await Deployment.RunAsync(() => 
{
    var sink1 = new Enum.Res("sink1", new()
    {
        IntEnum = Enum.IntEnum.IntOne,
        StringEnum = Enum.StringEnum.StringTwo,
    });

    var sink2 = new Enum.Mod.Res("sink2", new()
    {
        IntEnum = Enum.Mod.IntEnum.IntOne,
        StringEnum = Enum.Mod.StringEnum.StringTwo,
    });

    var sink3 = new Enum.Mod.Nested.Res("sink3", new()
    {
        IntEnum = Enum.Mod.Nested.IntEnum.IntOne,
        StringEnum = Enum.Mod.Nested.StringEnum.StringTwo,
    });

    var sink4 = new Enum.Deluxe("sink4", new()
    {
        NumberEnum = Enum.NumberEnum.ZeroPointOne,
        WordyEnum = Enum.WordyEnum.It_s_got_apostrophes,
        ArrayOfEnum = new[]
        {
            Enum.StringEnum.StringOne,
            Enum.StringEnum.StringTwo,
        },
        MapOfEnum = 
        {
            { "small", Enum.IntEnum.IntOne },
            { "large", Enum.IntEnum.IntTwo },
        },
        Holder = new Enum.Inputs.HolderArgs
        {
            Size = Enum.IntEnum.IntTwo,
            Color = Enum.StringEnum.StringOne,
        },
        UnionEnum = Enum.WordyEnum.A_Value_With_Spaces_,
    });

});


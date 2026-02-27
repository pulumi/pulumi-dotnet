using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Enum = Pulumi.Enum;
using Extenumref = Pulumi.Extenumref;

return await Deployment.RunAsync(() => 
{
    var myRes = new Enum.Res("myRes", new()
    {
        IntEnum = Enum.IntEnum.IntOne,
        StringEnum = Enum.StringEnum.StringOne,
    });

    var mySink = new Extenumref.Sink("mySink", new()
    {
        StringEnum = Enum.StringEnum.StringTwo,
    });

});


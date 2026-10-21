using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Docs = Pulumi.Docs;
using Enum = Pulumi.Enum;

return await Deployment.RunAsync(() => 
{
    var enumRes = new Enum.Res("enumRes", new()
    {
        IntEnum = Enum.IntEnum.IntOne,
        StringEnum = Enum.StringEnum.StringOne,
    });

    var res = new Docs.Resource("res", new()
    {
        In = Docs.Fun.Invoke(new()
        {
            In = false,
        }).Apply(invoke => invoke.Out),
        ExternalEnum = Enum.StringEnum.StringOne,
    });

});


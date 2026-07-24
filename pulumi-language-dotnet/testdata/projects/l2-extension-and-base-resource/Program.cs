using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Extbase = Pulumi.Extbase;
using Myext = Pulumi.Myext;

return await Deployment.RunAsync(() => 
{
    var greeting = new Myext.Greeting("greeting");

    var @base = new Extbase.Base("base");

    return new Dictionary<string, object?>
    {
        ["parameterValue"] = greeting.ParameterValue,
        ["baseValue"] = @base.BaseValue,
    };
});


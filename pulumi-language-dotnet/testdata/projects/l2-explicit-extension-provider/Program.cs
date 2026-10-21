using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Extbase = Pulumi.Extbase;
using Myext = Pulumi.Myext;

return await Deployment.RunAsync(() => 
{
    var prov = new Extbase.Provider("prov");

    var greeting = new Myext.Greeting("greeting", new()
    {
    }, new CustomResourceOptions
    {
        Provider = prov,
    });

    var @base = new Extbase.Base("base", new()
    {
    }, new CustomResourceOptions
    {
        Provider = prov,
    });

    return new Dictionary<string, object?>
    {
        ["parameterValue"] = greeting.ParameterValue,
        ["baseValue"] = @base.BaseValue,
    };
});


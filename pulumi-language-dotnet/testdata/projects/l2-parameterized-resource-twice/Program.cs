using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Byepackage = Pulumi.Byepackage;
using Hipackage = Pulumi.Hipackage;

return await Deployment.RunAsync(() => 
{
    // The resource name is based on the parameter value
    var example1 = new Hipackage.HelloWorld("example1");

    var exampleComponent1 = new Hipackage.HelloWorldComponent("exampleComponent1");

    // The resource name is based on the parameter value
    var example2 = new Byepackage.GoodbyeWorld("example2");

    var exampleComponent2 = new Byepackage.GoodbyeWorldComponent("exampleComponent2");

    return new Dictionary<string, object?>
    {
        ["parameterValue1"] = example1.ParameterValue,
        ["parameterValueFromComponent1"] = exampleComponent1.ParameterValue,
        ["parameterValue2"] = example2.ParameterValue,
        ["parameterValueFromComponent2"] = exampleComponent2.ParameterValue,
    };
});


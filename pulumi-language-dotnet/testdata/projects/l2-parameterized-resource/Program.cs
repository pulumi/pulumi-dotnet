using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Subpackage = Pulumi.Subpackage;

return await Deployment.RunAsync(() => 
{
    // The resource name is based on the parameter value
    var example = new Subpackage.HelloWorld("example");

    var exampleComponent = new Subpackage.HelloWorldComponent("exampleComponent");

    return new Dictionary<string, object?>
    {
        ["parameterValue"] = example.ParameterValue,
        ["parameterValueFromComponent"] = exampleComponent.ParameterValue,
    };
});


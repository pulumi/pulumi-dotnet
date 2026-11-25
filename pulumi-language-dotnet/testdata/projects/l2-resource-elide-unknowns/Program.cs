using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Output_ = Pulumi.Output_;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    // This test checks that when a provider doesn't return properties for fields it considers unknown the runtime
    // can still access that field as an output.
    var prov = new Output_.Provider("prov", new()
    {
        ElideUnknowns = true,
    });

    var unknown = new Output_.Resource("unknown", new()
    {
        Value = 1.0,
    }, new CustomResourceOptions
    {
        Provider = prov,
    });

    var complex = new Output_.ComplexResource("complex", new()
    {
        Value = 1.0,
    }, new CustomResourceOptions
    {
        Provider = prov,
    });

    // Try and use the unknown output as an input to another resource to check that it doesn't cause any issues.
    var res = new Simple.Resource("res", new()
    {
        Value = unknown.Output.Apply(output => output == "hello"),
    });

    var resArray = new Simple.Resource("resArray", new()
    {
        Value = complex.OutputArray.Apply(outputArray => outputArray[0] == "hello"),
    });

    var resMap = new Simple.Resource("resMap", new()
    {
        Value = complex.OutputMap.Apply(outputMap => outputMap["x"] == "hello"),
    });

    var resObject = new Simple.Resource("resObject", new()
    {
        Value = complex.OutputObject.Apply(outputObject => outputObject.Output == "hello"),
    });

    return new Dictionary<string, object?>
    {
        ["out"] = unknown.Output,
        ["outArray"] = complex.OutputArray.Apply(outputArray => outputArray[0]),
        ["outMap"] = complex.OutputMap.Apply(outputMap => outputMap["x"]),
        ["outObject"] = complex.OutputObject.Apply(outputObject => outputObject.Output),
    };
});


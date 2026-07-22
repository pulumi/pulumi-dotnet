using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Myext = Pulumi.Myext;

return await Deployment.RunAsync(() => 
{
    var greeting = new Myext.Greeting("greeting");

    var greetingComp = new Myext.GreetingComponent("greetingComp");

    return new Dictionary<string, object?>
    {
        ["parameterValue"] = greeting.ParameterValue,
        ["parameterValueFromComponent"] = greetingComp.ParameterValue,
        ["invokeGreeting"] = Myext.Greet.Invoke(new()
        {
            Name = "Pulumi",
        }).Apply(invoke => invoke.Greeting),
    };
});


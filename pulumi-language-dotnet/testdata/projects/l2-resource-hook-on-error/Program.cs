using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Flaky = Pulumi.Flaky;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var hookTestFile = config.Require("hookTestFile");
    var res = new Flaky.FlakyCreate("res", new()
    {
    });

});


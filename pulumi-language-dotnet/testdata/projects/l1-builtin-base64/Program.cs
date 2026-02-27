using System;
using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var input = config.Require("input");
    var bytes = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(input));

    return new Dictionary<string, object?>
    {
        ["data"] = bytes,
        ["roundtrip"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(bytes)),
    };
});


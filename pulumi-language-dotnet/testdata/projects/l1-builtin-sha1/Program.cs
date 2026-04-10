using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Pulumi;

	
string ComputeSHA1(string input) 
{
    var hash = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(input));
    return BitConverter.ToString(hash).Replace("-","").ToLowerInvariant();
}

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var input = config.Require("input");
    var hash = ComputeSHA1(input);

    return new Dictionary<string, object?>
    {
        ["hash"] = hash,
    };
});


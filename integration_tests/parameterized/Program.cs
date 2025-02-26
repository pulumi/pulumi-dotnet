// Copyright 2022-2023, Pulumi Corporation.  All rights reserved.

using System.Collections.Generic;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.Pkg;

class Program
{
    static Task<int> Main(string[] args)
    {
        return Deployment.RunAsync(() =>
        {
            var customA = new Echo("a", new EchoArgs { Value = 42 });

            return new Dictionary<string, object?>
            {
                {  "echoA", customA.Value },
            };
        });
    }
}
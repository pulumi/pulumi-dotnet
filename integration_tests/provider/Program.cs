// Copyright 2022-2023, Pulumi Corporation.  All rights reserved.

using System.Collections.Generic;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.Testprovider;

class Program
{
    static Task<int> Main(string[] args)
    {
        return Deployment.RunAsync(() =>
        {
            var customA = new Echo("a", new EchoArgs { Value = 42 });
            var customB = new Echo("b", new EchoArgs { Value = "hello" });
            var customC = new Echo("c", new EchoArgs { Value = new object[] { 1, "goodbye", true} });

            return new Dictionary<string, object?>
            {
                {  "echoA", customA.Value },
                {  "echoB", customB.Value },
                {  "echoC", customC.Value },
            };
        });
    }
}
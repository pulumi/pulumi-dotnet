// Copyright 2022-2023, Pulumi Corporation.  All rights reserved.

using System.Collections.Generic;
using System.Threading.Tasks;
using Pulumi;

class Program
{
    static Task<int> Main(string[] args)
    {
        return Deployment.RunAsync(() =>
        {
            var customA = new TestResource("a", new TestResourceArgs { Echo = 42 });
            var customB = new TestResource("b", new TestResourceArgs { Echo = "hello" });
            var customC = new TestResource("c", new TestResourceArgs { Echo = new object[] { 1, "goodbye", true} });

            return new Dictionary<string, object?>
            {
                {  "echoA", customA.Echo },
                {  "echoB", customB.Echo },
                {  "echoC", customC.Echo },
            };
        });
    }
}
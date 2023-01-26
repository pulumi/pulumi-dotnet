// Copyright 2021, Pulumi Corporation.  All rights reserved.

using System;
using System.Threading.Tasks;
using Pulumi;

class Program
{
    static Task<int> Main(string[] args)
    {
        return Deployment.RunAsync(() => {
            Console.WriteLine("With sln");
        });
    }
}

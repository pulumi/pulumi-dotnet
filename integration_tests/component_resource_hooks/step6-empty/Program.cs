// Copyright 2025, Pulumi Corporation.  All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Pulumi;

class ResourceHooksStack : Stack
{
    public ResourceHooksStack()
    {
    }
}

class Program
{
    static Task<int> Main(string[] args) => Deployment.RunAsync<ResourceHooksStack>();
}

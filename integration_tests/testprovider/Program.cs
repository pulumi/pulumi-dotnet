// Copyright 2022-2023, Pulumi Corporation.  All rights reserved.
using System.Threading;
using System.Threading.Tasks;
using Pulumi.Experimental.Provider;


public static class Program {
    public static Task Main(string[] args) {
        return Provider.Serve(args, host => new TestProvider(host), CancellationToken.None);
    }
}
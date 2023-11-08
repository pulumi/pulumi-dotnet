// Copyright 2025, Pulumi Corporation.  All rights reserved.
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System.Linq;
using Pulumi.Experimental.Provider;


public static class Program {

    public static InferredProvider CreateProvider(IHost host) {
        var parameter = (ParameterizeRequest request, CancellationToken ct) => {
            string parameter;
            if (request.Parameters is ParametersArgs args)
            {
                if (args.Args.Length != 1)
                {
                    throw new Exception("expected exactly one argument");
                }
                parameter = args.Args[0];
            }
            else if (request.Parameters is ParametersValue value)
            {
                parameter = System.Text.Encoding.UTF8.GetString(value.Value.ToArray());
            }
            else
            {
                throw new Exception("unexpected parameter type");
            }

            return Task.FromResult(new InferredProvider(
                parameter,
                "1.0.0",
                ProviderCrate.New(new TestProvider(host)),
                ImmutableDictionary<string, CustomResourceCrate>.Empty.Add(
                    "index:Echo", CustomResourceCrate.New(new EchoResource(host))
                )
            ) as Provider);
        };

        return new InferredProvider(
            "testprovider",
            "0.0.1",
            ProviderCrate.New(new TestProvider(host)),
            ImmutableDictionary<string, CustomResourceCrate>.Empty.Add(
                "index:Echo", CustomResourceCrate.New(new EchoResource(host))
            ),
            parameter
        );
    }

    public static Task Main(string[] args) {
        return Provider.Serve(
            args, 
            "0.0.1",
            CreateProvider,
            CancellationToken.None);
    }
}
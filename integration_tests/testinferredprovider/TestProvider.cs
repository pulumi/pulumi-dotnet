// Copyright 2025, Pulumi Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.Experimental.Provider;

record TestProviderArgs();

class TestProvider : Provider<TestProviderArgs>
{
    readonly IHost host;

    public TestProvider(IHost host)
    {
        this.host = host;
    }
}

public sealed class EchoArgs
{
    public object? Value { get; set; } = null;
}

class EchoResource : CustomResource<EchoArgs, EchoArgs, EchoArgs>
{
    readonly IHost host;

    public EchoResource(IHost host)
    {
        this.host = host;
    }

    public override Task<CreateResponse<EchoArgs>> Create(CreateRequest<EchoArgs> request, CancellationToken ct)
    {
        return Task.FromResult(new CreateResponse<EchoArgs>() { Id = request.Urn, Properties = request.Properties });
    }

    public override Task<CheckResponse<EchoArgs>> Check(CheckRequest<EchoArgs, EchoArgs> request, CancellationToken ct)
    {
        return Task.FromResult(new CheckResponse<EchoArgs>() { Inputs = request.NewInputs });
    }

    public override Task Delete(DeleteRequest<EchoArgs> request, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}

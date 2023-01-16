// Copyright 2022-2023, Pulumi Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.Experimental.Provider;

public class TestProvider : Provider {
    readonly IHost host;
    int id = 0;

    public TestProvider(IHost host) {
        this.host = host;
    }

    public override Task<GetPluginInfoResponse> GetPluginInfo(CancellationToken ct)
    {
        return Task.FromResult(new GetPluginInfoResponse() {
            Version = "0.0.1",
        });
    }

    public override Task<CheckResponse> CheckConfig(CheckRequest request, CancellationToken ct)
    {
        return Task.FromResult(new CheckResponse() { Inputs = request.News });
    }

    public override Task<DiffResponse> DiffConfig(DiffRequest request, CancellationToken ct)
    {
        return Task.FromResult(new DiffResponse());
    }

    public override Task<ConfigureResponse> Configure(ConfigureRequest request, CancellationToken ct)
    {
        return Task.FromResult(new ConfigureResponse());
    }

    public override Task<CheckResponse> Check(CheckRequest request, CancellationToken ct)
    {
        if (request.Type == "testprovider:index:Echo" ||
            request.Type == "testprovider:index:Random" ||
            request.Type == "testprovider:index:FailsOnDelete")
        {
            return Task.FromResult(new CheckResponse() { Inputs = request.News });
        }

        throw new Exception($"Unknown resource type '{request.Type}'");
    }

    public override Task<DiffResponse> Diff(DiffRequest request, CancellationToken ct)
    {
        if (request.Type == "testprovider:index:Echo") {
            var changes = !request.Olds["echo"].Equals(request.News["echo"]);
            return Task.FromResult(new DiffResponse() {
                Changes = changes,
                Replaces = new string[] { "echo" },
            });
        }
        else if (request.Type == "testprovider:index:Random")
        {
            var changes = !request.Olds["length"].Equals(request.News["length"]);
            return Task.FromResult(new DiffResponse() {
                Changes = changes,
                Replaces = new string[] { "length" },
            });
        }
        else if (request.Type == "testprovider:index:FailsOnDelete")
        {
            return Task.FromResult(new DiffResponse() {
                Changes = false,
            });
        }

        throw new Exception($"Unknown resource type '{request.Type}'");
    }

    private static string makeRandom(int length)
    {
        var charset = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var rand = new System.Random();
        var result = new char[length];
        for (var i = 0; i < length; ++i)
        {
            result[i] = charset[rand.Next(charset.Length)];
        }
        return new string(result);
    }

    public override Task<CreateResponse> Create(CreateRequest request, CancellationToken ct)
    {
        if (request.Type == "testprovider:index:Echo") {
            var outputs = new Dictionary<string, PropertyValue>();
            outputs.Add("echo", request.Properties["echo"]);

            ++this.id;
            return Task.FromResult(new CreateResponse() {
                Id = this.id.ToString(),
                Properties = outputs,
            });
        }
        else if (request.Type == "testprovider:index:Random")
        {
            var length = request.Properties["length"];
            if (!length.TryGetNumber(out var number))
            {
                throw new Exception($"Expected input property 'length' of type 'number' but got '{length.Type}'");
            }

            // Actually "create" the random number
            var result = makeRandom((int)number);

            var outputs = new Dictionary<string, PropertyValue>();
            outputs.Add("length", length);
            outputs.Add("result", new PropertyValue(result));

            return Task.FromResult(new CreateResponse() {
                Id = result,
                Properties = outputs,
            });
        }
        else if (request.Type == "testprovider:index:FailsOnDelete")
        {
            ++this.id;
            return Task.FromResult(new CreateResponse() {
                Id = this.id.ToString(),
            });
        }

        throw new Exception($"Unknown resource type '{request.Type}'");
    }

    public override Task Delete(DeleteRequest request, CancellationToken ct)
    {
        if (request.Type == "testprovider:index:FailsOnDelete")
        {
            throw new Exception("Delete always fails for the FailsOnDelete resource");
        }

        return Task.CompletedTask;
    }

    public override Task<ReadResponse> Read(ReadRequest request, CancellationToken ct)
    {
        var response = new ReadResponse() {
            Id = request.Id,
            Properties = request.Properties,
        };
        return Task.FromResult(response);
    }
}

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

    public override Task<GetSchemaResponse> GetSchema(GetSchemaRequest request, CancellationToken ct)
    {
        var schema = """
{
    "name": "testprovider",
    "version": "1.0.0",
    "meta": {
        "supportPack": true
    },
    "resources": {
        "testprovider:index:Echo": {
            "description": "A test resource that echoes its input.",
            "properties": {
                "value": {
                    "$ref": "pulumi.json#/Any",
                    "description": "Input to echo."
                }
            },
            "inputProperties": {
                "value": {
                    "$ref": "pulumi.json#/Any",
                    "description": "Input to echo."
                }
            },
            "type": "object"
        }
    }
}
""";

        return Task.FromResult(new GetSchemaResponse()
        {
            Schema = schema,
        });
    }

    public override Task<CheckResponse> CheckConfig(CheckRequest request, CancellationToken ct)
    {
        return Task.FromResult(new CheckResponse() { Inputs = request.NewInputs });
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
            return Task.FromResult(new CheckResponse() { Inputs = request.NewInputs });
        }

        throw new Exception($"Unknown resource type '{request.Type}'");
    }

    public override Task<DiffResponse> Diff(DiffRequest request, CancellationToken ct)
    {
        if (request.Type == "testprovider:index:Echo") {
            var changes = !request.OldState["value"].Equals(request.NewInputs["value"]);
            return Task.FromResult(new DiffResponse() {
                Changes = changes,
                Replaces = new string[] { "value" },
            });
        }
        else if (request.Type == "testprovider:index:Random")
        {
            var changes = !request.OldState["length"].Equals(request.NewInputs["length"]);
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
            outputs.Add("value", request.Properties["value"]);

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

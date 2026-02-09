// Copyright 2022-2025, Pulumi Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.Experimental;
using Pulumi.Experimental.Provider;

public class TestProvider : Provider
{
    readonly Pulumi.Experimental.IEngine host;
    int id = 0;
    string parameter;

    public TestProvider(Pulumi.Experimental.IEngine host)
    {
        this.host = host;
        this.parameter = "testprovider";
    }

    public override Task<ParameterizeResponse> Parameterize(ParameterizeRequest request, CancellationToken ct)
    {
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

        this.parameter = parameter;

        return Task.FromResult(new ParameterizeResponse(parameter, "1.0.0"));
    }

    public override Task<GetSchemaResponse> GetSchema(GetSchemaRequest request, CancellationToken ct)
    {
        var schema = @"
{
    ""name"": ""NAME"",
    ""version"": ""1.0.0"",
    ""meta"": {
        ""supportPack"": true
    },
    ""resources"": {
        ""NAME:index:Echo"": {
            ""description"": ""A test resource that echoes its input."",
            ""properties"": {
                ""value"": {
                    ""$ref"": ""pulumi.json#/Any"",
                    ""description"": ""Input to echo.""
                }
            },
            ""inputProperties"": {
                ""value"": {
                    ""$ref"": ""pulumi.json#/Any"",
                    ""description"": ""Input to echo.""
                }
            },
            ""type"": ""object""
        }
    }PARAM
}
";
        var parameterization = @"
,
    ""parameterization"": {
        ""baseProvider"": {
            ""name"": ""testprovider"",
            ""version"": ""0.0.1""
        },
        ""parameter"": ""UTFBYTES""
    }
";

        if (parameter == "testprovider")
        {
            parameterization = "";
        }

        // Very hacky schema build just to test out that parameterization calls are made
        parameterization = parameterization.Replace("UTFBYTES", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(parameter)));
        schema = schema.Replace("NAME", parameter);
        schema = schema.Replace("PARAM", parameterization);

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
        if (request.Type == parameter + ":index:Echo" ||
            request.Type == parameter + ":index:Random" ||
            request.Type == parameter + ":index:FailsOnDelete" ||
            request.Type == parameter + ":index:Updatable")
        {
            return Task.FromResult(new CheckResponse() { Inputs = request.NewInputs });
        }

        throw new Exception($"Unknown resource type '{request.Type}'");
    }

    public override Task<DiffResponse> Diff(DiffRequest request, CancellationToken ct)
    {
        if (request.Type == parameter + ":index:Echo") {
            var changes = !request.OldOutputs["value"].Equals(request.NewInputs["value"]);
            return Task.FromResult(new DiffResponse() {
                Changes = changes,
                Replaces = new string[] { "value" },
            });
        }
        else if (request.Type == parameter + ":index:Random")
        {
            var changes = !request.OldOutputs["length"].Equals(request.NewInputs["length"]);
            return Task.FromResult(new DiffResponse() {
                Changes = changes,
                Replaces = new string[] { "length" },
            });
        }
        else if (request.Type == parameter + ":index:FailsOnDelete")
        {
            return Task.FromResult(new DiffResponse() {
                Changes = false,
            });
        }
        else if (request.Type == parameter + ":index:Updatable")
        {
            var changes = !request.OldInputs["value"].Equals(request.NewInputs["value"]);
            return Task.FromResult(new DiffResponse() {
                Changes = changes,
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
        if (request.Type == parameter + ":index:Echo")
        {
            var outputs = new Dictionary<string, PropertyValue>();
            outputs.Add("value", request.Inputs["value"]);

            ++this.id;
            return Task.FromResult(new CreateResponse() {
                Id = this.id.ToString(),
                Outputs = outputs,
            });
        }
        else if (request.Type == parameter + ":index:Random")
        {
            var length = request.Inputs["length"];
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
                Outputs = outputs,
            });
        }
        else if (request.Type == parameter + ":index:FailsOnDelete")
        {
            ++this.id;
            return Task.FromResult(new CreateResponse() {
                Id = this.id.ToString(),
            });
        }
        else if (request.Type == parameter + ":index:Updatable")
        {
            var outputs = new Dictionary<string, PropertyValue>();
            outputs.Add("value", request.Inputs["value"]);

            ++this.id;
            return Task.FromResult(new CreateResponse() {
                Id = this.id.ToString(),
                Outputs = outputs,
            });
        }

        throw new Exception($"Unknown resource type '{request.Type}'");
    }

    public override Task<UpdateResponse> Update(UpdateRequest request, CancellationToken ct)
    {
        if (request.Type == parameter + ":index:Updatable")
        {
            var outputs = new Dictionary<string, PropertyValue>();
            outputs.Add("value", request.NewInputs["value"]);

            return Task.FromResult(new UpdateResponse() {
                Outputs = outputs,
            });
        }

        throw new Exception($"Unknown resource type '{request.Type}'");
    }

    public override Task Delete(DeleteRequest request, CancellationToken ct)
    {
        if (request.Type == parameter + ":index:FailsOnDelete")
        {
            throw new Exception("Delete always fails for the FailsOnDelete resource");
        }

        return Task.CompletedTask;
    }

    public override Task<ReadResponse> Read(ReadRequest request, CancellationToken ct)
    {
        var response = new ReadResponse() {
            Id = request.Id,
            Outputs = request.Inputs,
        };
        return Task.FromResult(response);
    }

    public override Task<InvokeResponse> Invoke(InvokeRequest request, CancellationToken ct)
    {
        var response = new InvokeResponse();

        if (request.Tok == "testprovider:index:returnArgs")
        {
            response.Return = request.Args;
        }

        return Task.FromResult(response);
    }
}

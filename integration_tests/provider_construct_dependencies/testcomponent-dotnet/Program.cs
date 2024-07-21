using System;
using System.Threading;
using System.Threading.Tasks;
using Pulumi.Experimental.Provider;

public class Provider : ComponentResourceProviderBase
{
    public override Task<GetSchemaResponse> GetSchema(GetSchemaRequest request, CancellationToken ct)
    {
        return Task.FromResult(new GetSchemaResponse()
        {
        });
    }

    public override Task<ConstructResponse> Construct(ConstructRequest request, CancellationToken ct)
    {
        return request.Type switch
        {
            "test:index:Test" => Construct<Component, ComponentArgs>(request,
                (name, args, options) => Task.FromResult(new Component(name, args, options))),
            _ => throw new NotImplementedException()
        };
    }

    public override Task<ConfigureResponse> Configure(ConfigureRequest request, CancellationToken ct)
    {
        return Task.FromResult(new ConfigureResponse()
        {
            AcceptOutputs = true,
            AcceptResources = true,
            AcceptSecrets = true,
            SupportsPreview = true
        });
    }
}

class Program
{
    public static Task Main(string []args) =>
        Pulumi.Experimental.Provider.Provider.Serve(args, "0.0.1", host => new Provider(), CancellationToken.None);
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Pulumi.Experimental.Provider;
using Pulumi.IntegrationTests.Utils;

namespace TestProvider;

public class TestProviderImpl : ComponentResourceProviderBase
{
    public override Task<ConstructResponse> Construct(ConstructRequest request, CancellationToken ct)
    {
        return request.Type switch
        {
            "test:index:Test" => Construct<ComponentArgs, Component>(request,
                (name, args, options) => Task.FromResult(new Component(name, args, options))),
            _ => throw new NotImplementedException()
        };
    }
}

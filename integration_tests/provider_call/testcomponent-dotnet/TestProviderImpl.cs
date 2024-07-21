using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pulumi.Experimental.Provider;

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

    public override Task<CallResponse> Call(CallRequest request, CancellationToken ct)
    {
        return request.Tok switch
        {
            "test:index:testFunction" => Call<TestFunctionArgs, TestFunctionResult>(request, CheckInput, TestFunctionImpl),
            _ => throw new NotImplementedException()
        };
    }

    private CheckResult CheckInput(ResourceReference? resourceReference, TestFunctionArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.TestValue))
        {
            return new CheckResult(new List<CheckFailure>
            {
                new(nameof(args.TestValue), "Args mut not be empty")
            });
        }

        return CheckResult.Empty;
    }

    private async Task<TestFunctionResult> TestFunctionImpl(ResourceReference? resourceReference, TestFunctionArgs args)
    {
        await Task.Delay(10);
        return new TestFunctionResult(resourceReference?.URN ?? "", args.TestValue);
    }
}

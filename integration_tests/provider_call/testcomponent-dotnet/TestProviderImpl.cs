using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pulumi;
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

    public override Task<CallResponse> Call(CallRequest request, CancellationToken ct)
    {
        return request.Tok switch
        {
            "test:index:testFunction" => Call<TestFunctionArgs, TestFunctionResult>(request, CheckInput, TestFunctionImpl),
            _ => throw new NotImplementedException()
        };
    }

    private Output<CheckResult> CheckInput(ResourceReference? resourceReference, TestFunctionArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.TestValue))
        {
            return Output.Create(new CheckResult(new List<CheckFailure>
            {
                new(nameof(args.TestValue), "Args mut not be empty")
            }));
        }

        return Output.Create(CheckResult.Empty);
    }

    private Output<TestFunctionResult> TestFunctionImpl(ResourceReference? resourceReference, TestFunctionArgs args)
    {
        return Output.Create(Task.Delay(10).ContinueWith(t => new TestFunctionResult(resourceReference?.URN ?? "", args.TestValue)));
    }
}

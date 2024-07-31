using System.Threading.Tasks;
using Pulumi;

public static class TestFunction
{
    public static Output<TestFunctionResult> Call(TestFunctionArgs args, Resource? resource = null, CallOptions? options = null)
        => Deployment.Instance.Call<TestFunctionResult>("test:index:testFunction", args, resource, options);
}

public sealed class TestFunctionArgs : CallArgs
{
    [Input("testValue", required: true)]
    public string TestValue { get; set; } = null!;
}

[OutputType]
public sealed class TestFunctionResult
{
    [Output("selfUrn")]
    public string SelfUrn { get; set; }

    [Output("testValue")]
    public string TestValue { get; set; }

    [OutputConstructor]
    public TestFunctionResult(string selfUrn, string testValue)
    {
        SelfUrn = selfUrn;
        TestValue = testValue;
    }
}

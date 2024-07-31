using Pulumi;

public sealed class TestFunctionArgs : InvokeArgs
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
        SelfUrn = selfUrn;
        TestValue = testValue;
    }
}

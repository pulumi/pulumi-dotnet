using Pulumi;

public class MyInvoke
{
    public void Invoke(InvokeArgs args, InvokeOptions? options = null)
    {
        var invokeResult = global::Pulumi.Deployment.Instance.Invoke<MyInvokeResult>(
            "testprovider:index:returnArgs", args, options
        );

        invokeResult.Apply(res =>
        {
            if (res.Length != 11)
            {
                throw new System.Exception("This is not 11, it's " + res.Length);
            }

            if (res.Prefix != "hello")
            {
                throw new System.Exception("This is not hello, it's " + res.Prefix);
            }

            return res;
        });
    }

    public sealed class MyInvokeArgs : global::Pulumi.InvokeArgs
    {
        [Input("prefix", required: true)]
        public string Prefix { get; set; } = null!;

        [Input("length", required: true)]
        public int Length { get; set; } = 123;

        public MyInvokeArgs()
        {
        }
        public static new MyInvokeArgs Empty => new MyInvokeArgs();
    }

    [OutputType]
    public sealed class MyInvokeResult
    {
        public readonly string Prefix;
        public readonly int Length;

        [OutputConstructor]
        public MyInvokeResult(string prefix, int length)
        {
            this.Length = length;
            this.Prefix = prefix;
        }
    }
}
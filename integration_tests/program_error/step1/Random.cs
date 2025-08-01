// Exposes the Random resource from the testprovider.

using Pulumi;

public partial class Random : Pulumi.CustomResource
{
    [Output("length")]
    public Output<int> Length { get; private set; } = null!;

    [Output("result")]
    public Output<string> Result { get; private set; } = null!;

    public Random(string name, RandomArgs args, CustomResourceOptions? options = null)
        : base("testprovider:index:Random", name, args ?? new RandomArgs(), options)
    {
    }
}

public sealed class RandomArgs : Pulumi.ResourceArgs
{
    [Input("length", required: true)]
    public Input<int> Length { get; set; } = null!;

    public RandomArgs()
    {
    }
}

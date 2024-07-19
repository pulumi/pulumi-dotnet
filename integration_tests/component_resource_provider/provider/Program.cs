using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.Experimental.Provider;

class ComponentArgs : ResourceArgs
{
    [Input("passwordLength")]
    public Input<int> PasswordLength { get; set; } = null!;
}

class Component : ComponentResource
{
    private static readonly char[] Chars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    public Output<string> PasswordResult { get; set; }

    public Component(string name, ComponentArgs args, ComponentResourceOptions? opts = null)
        : base("test:index:Tes", name, args, opts)
    {
        PasswordResult = args.PasswordLength.Apply(GenerateRandomString);
    }

    private static Output<string> GenerateRandomString(int length)
    {
        var result = new StringBuilder(length);
        var random = new Random();

        for (var i = 0; i < length; i++)
        {
            result.Append(Chars[random.Next(Chars.Length)]);
        }

        return Output.CreateSecret(result.ToString());
    }
}

public class Provider : ComponentResourceProviderBase
{
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
        Pulumi.Experimental.Provider.Provider.Serve(args, "1.0", host => new Provider(), CancellationToken.None);
}

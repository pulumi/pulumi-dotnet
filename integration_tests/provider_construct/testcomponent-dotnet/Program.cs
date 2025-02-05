using System.Threading;
using System.Threading.Tasks;
using TestProvider;

class Program
{
    public static Task Main(string []args)
    {
        var provider = new Pulumi.Experimental.Provider.ComponentProviderImplementation(null, "test");
        return Pulumi.Experimental.Provider.Provider.Serve(args, "0.0.1", host => provider, CancellationToken.None);
    }
}

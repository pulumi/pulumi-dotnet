using System.Threading;
using System.Threading.Tasks;

namespace TestProvider;

class Program
{
    public static Task Main(string []args) =>
        Pulumi.Experimental.Provider.Provider.Serve(args, "0.0.1", host => new TestProviderImpl(), CancellationToken.None);
}

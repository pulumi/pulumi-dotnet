using System.Threading;
using System.Threading.Tasks;
using Pulumi.Experimental.Provider;

class Program
{
    public static Task Main(string []args) => ComponentProviderHost.Serve(args);
}

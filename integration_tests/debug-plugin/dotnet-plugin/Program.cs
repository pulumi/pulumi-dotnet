using System.Threading;
using System.Threading.Tasks;
using Pulumi.Experimental.Provider;

namespace Pulumi.DebugProvider
{
    class Program
    {
        static Task Main(string[] args)
        {
		return Provider.Serve(args, null, host => new Provider(), CancellationToken.None);
        }
    }
}

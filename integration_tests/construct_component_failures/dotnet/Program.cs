using System.Threading.Tasks;
using FluentAssertions;
using Pulumi;
using Pulumi.Utilities;

using Pulumi.IntegrationTests.Utils;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var returnCode = await Deployment.RunAsync(async () =>
        {
            var component10 = new Component("component10", new ComponentArgs());
        });
	return returnCode;
    }

}

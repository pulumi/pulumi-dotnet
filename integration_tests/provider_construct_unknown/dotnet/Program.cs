using System.Threading.Tasks;
using Pulumi;
using Utils;

class Program
{
    private const string TestInput = "expectedInput";

    static Task<int> Main(string[] args) =>
        Deployment.RunAsync(() =>
        {
            DebuggerUtils.WaitForDebugger();
            var resource = new Random("resource", new RandomArgs
            {
                Length = 10,
            });


            var baseComponent = new Component("baseComponent", new ComponentArgs()
            {
                TestInput = resource.Id.Apply(id => $"TestInput {id}"),
            });
            return Task.CompletedTask;
        });
}

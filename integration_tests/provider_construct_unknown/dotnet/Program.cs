using System.Threading.Tasks;
using Pulumi;
#if IMPORT_UTILS
using Utils;
#endif

class Program
{
    static Task<int> Main(string[] args) =>
        Deployment.RunAsync(() =>
        {
            #if IMPORT_UTILS
            DebuggerUtils.WaitForDebugger();
            #endif

            var resource = new Random("resource", new RandomArgs
            {
                Length = 10,
            });

            new Component("baseComponent", new ComponentArgs
            {
                TestInput = resource.Id.Apply(id => $"TestInput {id}"),
                TestSecretInput = Output.CreateSecret(resource.Id.Apply(id => $"TestInput {id}")),
            });
            return Task.CompletedTask;
        });
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.IntegrationTests.Utils;

class Program
{
    static Task<int> Main(string[] args) =>
        Deployment.RunAsync(() =>
        {
            DebuggerUtils.WaitForDebugger();

            var resource = new Echo("resource", new EchoArgs
            {
                Value = "Dummy"
            });

            var component = new Component("baseComponent", new ComponentArgs
            {
                InputResource = resource
            });
            return new Dictionary<string, object?>
            {
                // disabled due to bug with deserialize resource refs
                // {"referenceResourceUrn", component.OutputResource.Apply(u => u.Urn)}
            };
        });
}

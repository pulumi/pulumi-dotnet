using System.Threading.Tasks;
using FluentAssertions;
using Pulumi;
using Pulumi.Utilities;
#if IMPORT_UTILS
using Utils;
#endif

class Program
{
    static async Task<int> Main(string[] args)
    {
        var returnCode = await Deployment.RunAsync(async () =>
        {
            #if IMPORT_UTILS
            DebuggerUtils.WaitForDebugger();
            #endif

            var component = new Component("testComponent", new ComponentArgs());

            var testValue = System.Environment.GetEnvironmentVariable("TEST_VALUE") ?? "HelloW World";

            var args = new TestFunctionArgs()
            {
                TestValue = testValue
            };

            var resultOutput = TestFunction.Call(args, component);

            if (string.IsNullOrEmpty(testValue))
            {
                var result = await OutputUtilities.GetValueAsync(resultOutput);
                result.TestValue.Should().Be(args.TestValue);
                var expectedUrn  = await OutputUtilities.GetValueAsync(component.Urn);
                result.SelfUrn.Should().Be(expectedUrn);
            }
        });

        return returnCode;
    }
}

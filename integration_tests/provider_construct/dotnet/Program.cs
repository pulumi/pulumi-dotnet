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
            DebuggerUtils.WaitForDebugger();

            var complexArgs10 = new ComplexTypeArgs{ Name = "component10", IntValue = 100 };
            var component10 = new Component("component10", new ComponentArgs()
            {
                PasswordLength = 10,
                Complex = complexArgs10
            });
            var complexArgs20 = new ComplexTypeArgs{ Name = "component20", IntValue = 200 };
            var component20 = new Component("component20", new ComponentArgs()
            {
                PasswordLength = 20,
                Complex = complexArgs20
            });

            var result10 = await OutputUtilities.GetValueAsync(component10.PasswordResult);
            var result20 = await OutputUtilities.GetValueAsync(component20.PasswordResult);
            result10.Should().HaveLength(10);
            result20.Should().HaveLength(20);

            var complexResult10 = await OutputUtilities.GetValueAsync(component10.ComplexResult);
            var complexResult20 = await OutputUtilities.GetValueAsync(component20.ComplexResult);
            ValidateComplexResult(complexResult10, complexArgs10);
            ValidateComplexResult(complexResult20, complexArgs20);
        });

        return returnCode;
    }

    private static void ValidateComplexResult(ComplexType result, ComplexTypeArgs expected)
    {
        result.Name.Should().Be(expected.Name);
        result.IntValue.Should().Be(expected.IntValue);
    }
}

// Copyright 2016-2021, Pulumi Corporation.  All rights reserved.

using System.Threading.Tasks;
using FluentAssertions;
using Pulumi;
using Pulumi.Utilities;
using Utils;

class Program
{
    private const string TestInput = "expectedInput";

    static async Task<int> Main(string[] args)
    {
        var returnCode = await Deployment.RunAsync(async () =>
        {
            DebuggerUtils.WaitForDebugger();

            var baseComponent = new Component("baseComponent", new ComponentArgs()
            {
                TestInput = TestInput,
            });

            var dependentComponent = new Component("dependentComponent", new ComponentArgs()
            {
                TestInput = baseComponent.TestOutput
            });

            var result = await OutputUtilities.GetValueAsync(dependentComponent.TestOutput);
            result.Should().Be(TestInput);
        });

        return returnCode;
    }
}

// Copyright 2016-2021, Pulumi Corporation.  All rights reserved.

using System.Threading.Tasks;
using FluentAssertions;
using Pulumi;
using Pulumi.Utilities;
#if IMPORT_UTILS
using Utils;
#endif

class Program
{
    private const string TestInput = "expectedInput";

    static async Task<int> Main(string[] args)
    {
        var returnCode = await Deployment.RunAsync(async () =>
        {
            #if IMPORT_UTILS
            DebuggerUtils.WaitForDebugger();
            #endif

            var baseComponent = new Component("baseComponent", new ComponentArgs()
            {
                TestInput = TestInput,
                TestInputComplex = new ComplexTypeInput()
                {
                    Name = TestInput,
                    IntValue = TestInput.Length
                }
            });

            var dependentComponent = new Component("dependentComponent", new ComponentArgs()
            {
                TestInput = baseComponent.TestOutput,
                TestInputComplex = baseComponent.TestOutputComplex.Apply(a => new ComplexTypeInput
                {
                    Name = TestInput,
                    IntValue = TestInput.Length
                })
            });

            var result = await OutputUtilities.GetValueAsync(dependentComponent.TestOutput);
            result.Should().Be(TestInput);
        });

        return returnCode;
    }
}

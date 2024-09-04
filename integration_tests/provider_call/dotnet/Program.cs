// Copyright 2016-2021, Pulumi Corporation.  All rights reserved.

using System;
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

            var validArgs = new TestFunctionArgs()
            {
                TestValue = "Hello World"
            };

            var invalidArgs = new TestFunctionArgs()
            {
                TestValue = ""
            };

            var validResultOutput = TestFunction.Call(validArgs, component);

            var invalidResult = TestFunction.Call(invalidArgs, component);

            var validResult = await OutputUtilities.GetValueAsync(validResultOutput);
            validResult.TestValue.Should().Be(validArgs.TestValue);
            var expectedUrn  = await OutputUtilities.GetValueAsync(component.Urn);
            validResult.SelfUrn.Should().Be(expectedUrn);

            var awaitInvalid = async () => await OutputUtilities.GetValueAsync(invalidResult);
            await awaitInvalid.Should().ThrowAsync<Exception>();
        });

        return returnCode;
    }
}

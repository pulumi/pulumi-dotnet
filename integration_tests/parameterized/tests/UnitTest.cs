using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.Testing;
using Pulumi.Pkg;

namespace Parameterized.Tests
{
    class MyMocks : IMocks
    {
        public Task<object> CallAsync(MockCallArgs args)
        {
            return Task.FromResult<object>(args);
        }

        public Task<(string? id, object state)> NewResourceAsync(MockResourceArgs args)
        {
            return Task.FromResult<(string?, object)>(
                (args.Name + "_id", args.Inputs));
        }
    }

    class MyStack : Stack
    {
        public MyStack()
        {
            var resource = new Echo("a", new EchoArgs { Value = 42 });
        }
    }


    [TestClass]
    public class UnitTest
    {
        [TestMethod]
        public async void TestParamaterized()
        {
            var resources = await Deployment.TestAsync<MyStack>(
                new MyMocks(),
                new TestOptions()
            );
        }
    }
}
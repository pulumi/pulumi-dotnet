// Copyright 2016-2024, Pulumi Corporation

using System;
using Xunit;

namespace Pulumi.Tests.Resources
{
    public class CustomTimeoutsTests
    {
        [Fact]
        public void TestDeserialize()
        {
            var rpc = new Pulumirpc.RegisterResourceRequest.Types.CustomTimeouts
            {
                Create = "1m",
                Update = "1h3m12.99ms100us",
                Delete = ""
            };

            var timeouts = CustomTimeouts.Deserialize(rpc);
            Assert.Equal(TimeSpan.FromMinutes(1), timeouts.Create);
            Assert.Equal(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(3) + TimeSpan.FromMilliseconds(12.99) + TimeSpan.FromTicks(1), timeouts.Update);
            Assert.Null(timeouts.Delete);
        }
    }
}

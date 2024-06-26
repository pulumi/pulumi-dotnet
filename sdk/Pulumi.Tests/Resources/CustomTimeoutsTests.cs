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
                Update = "1h3m12.99ms1us100ns",
                Delete = ""
            };

            var timeouts = CustomTimeouts.Deserialize(rpc);
            Assert.Equal(TimeSpan.FromMinutes(1), timeouts.Create);
            Assert.Equal(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(3) + TimeSpan.FromMilliseconds(12.99) + TimeSpan.FromMilliseconds(1.0 / 1000.0) + TimeSpan.FromMilliseconds(100.0 / 1000_000.0), timeouts.Update);
            Assert.Null(timeouts.Delete);
        }

        [Fact]
        public void TestSerializationRoundtrip()
        {
            var timeouts = new CustomTimeouts()
            {
                Create = TimeSpan.FromHours(1) + TimeSpan.FromMinutes(3) + TimeSpan.FromMilliseconds(12.99) + TimeSpan.FromMilliseconds(1.0 / 1000.0) + TimeSpan.FromMilliseconds(100.0 / 1000_000.0),
                Update = TimeSpan.FromTicks(123456789),
                Delete = TimeSpan.FromHours(1) + TimeSpan.FromMilliseconds(100.0 / 1000_000.0)
            };
            var serialized = timeouts.Serialize();
            var deserialized = CustomTimeouts.Deserialize(serialized);

            Assert.Equal(timeouts.Create, deserialized.Create);
            Assert.Equal(timeouts.Update, deserialized.Update);
            Assert.Equal(timeouts.Delete, deserialized.Delete);
        }
    }
}

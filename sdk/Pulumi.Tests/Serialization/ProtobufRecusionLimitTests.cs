// Copyright 2016-2024, Pulumi Corporation

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Xunit;

namespace Pulumi.Tests.Serialization
{
    public class ProtobufRecursionLimitTests
    {
        [Fact]
        public void InvokeResponseRecursionLimitTest()
        {
            var response = new Pulumirpc.InvokeResponse
            {
                Return = GenerateNestedStruct(50),
            };
            var serialized = response.ToByteArray();

            // If we haven't successfully fixed the recursion limit,
            // this will throw due to hitting the limit.
            var actual = Pulumirpc.InvokeResponse.Parser.ParseFrom(serialized);

            // While we're here, verify it round-trips to the same serialized value.
            Assert.Equal(serialized, actual.ToByteArray());

            static Struct GenerateNestedStruct(int depth)
            {
                var s = new Struct();
                if (depth > 0)
                {
                    s.Fields.Add("Level" + depth, Value.ForStruct(GenerateNestedStruct(depth - 1)));
                }
                return s;
            }
        }
    }
}
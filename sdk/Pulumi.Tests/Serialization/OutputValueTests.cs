// Copyright 2016-2024, Pulumi Corporation

using System.Collections.Immutable;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Pulumi.Serialization;
using Xunit;

namespace Pulumi.Tests.Serialization
{
    public class OutputValueTests : ConverterTests
    {
        static Value CreateOutputValue(ImmutableHashSet<string> resources, Value? value = null, bool isSecret = false)
        {
            var result = new Value
            {
                StructValue = new Struct
                {
                    Fields =
                    {
                        { Constants.SpecialSigKey, new Value { StringValue = Constants.SpecialOutputValueSig } },
                    }
                }
            };
            if (value is not null)
            {
                result.StructValue.Fields.Add(Constants.ValueName, value);
            }
            if (isSecret)
            {
                result.StructValue.Fields.Add(Constants.SecretName, new Value { BoolValue = true });
            }
            if (resources.Count > 0)
            {
                var dependencies = new Value { ListValue = new ListValue() };
                foreach (var resource in resources)
                {
                    dependencies.ListValue.Values.Add(new Value { StringValue = resource });
                }
                result.StructValue.Fields.Add(Constants.DependenciesName, dependencies);
            }
            return result;
        }

        [Fact]
        public async Task TestUnknown()
        {
            var input = CreateOutputValue(ImmutableHashSet<string>.Empty);
            var result = Deserializer.Deserialize(input);
            var o = Assert.IsType<Output<object>>(result.Value);
            var data = await o.DataTask;
            Assert.Null(data.Value);
            Assert.False(data.IsKnown);
            Assert.False(data.IsSecret);
            Assert.Empty(data.Resources);
        }

        [Fact]
        public async Task TestString()
        {
            var input = CreateOutputValue(ImmutableHashSet<string>.Empty, new Value { StringValue = "hello" });
            var result = Deserializer.Deserialize(input);
            var o = Assert.IsType<Output<string>>(result.Value);
            var data = await o.DataTask;
            Assert.Equal("hello", data.Value);
            Assert.True(data.IsKnown);
            Assert.False(data.IsSecret);
            Assert.Empty(data.Resources);
        }

        [Fact]
        public async Task TestStringSecret()
        {
            var input = CreateOutputValue(
                ImmutableHashSet<string>.Empty, new Value { StringValue = "hello" }, isSecret: true);
            var result = Deserializer.Deserialize(input);
            var o = Assert.IsType<Output<string>>(result.Value);
            var data = await o.DataTask;
            Assert.Equal("hello", data.Value);
            Assert.True(data.IsKnown);
            Assert.True(data.IsSecret);
            Assert.Empty(data.Resources);
        }

        [Fact]
        public async Task TestStringDependencies()
        {
            var input = CreateOutputValue(
                ImmutableHashSet<string>.Empty.Add("foo"), new Value { StringValue = "hello" });
            var result = Deserializer.Deserialize(input);
            var o = Assert.IsType<Output<string>>(result.Value);
            var data = await o.DataTask;
            Assert.Equal("hello", data.Value);
            Assert.True(data.IsKnown);
            Assert.False(data.IsSecret);
            var resources = ImmutableHashSet<string>.Empty;
            foreach (var resource in data.Resources)
            {
                var urn = await resource.Urn.DataTask;
                resources = resources.Add(urn.Value);
            }
            Assert.Equal(ImmutableHashSet<string>.Empty.Add("foo"), resources);
        }

        [Fact]
        public async Task TestList()
        {
            var input = CreateOutputValue(ImmutableHashSet<string>.Empty, new Value
            {
                ListValue = new ListValue
                {
                    Values =
                    {
                        new Value { StringValue = "hello" },
                        new Value { StringValue = "world" },
                    }
                }
            });
            var result = Deserializer.Deserialize(input);
            var o = Assert.IsType<Output<ImmutableArray<object>>>(result.Value);
            var data = await o.DataTask;
            Assert.Equal(ImmutableArray<string>.Empty.Add("hello").Add("world"), data.Value);
            Assert.True(data.IsKnown);
            Assert.False(data.IsSecret);
            Assert.Empty(data.Resources);
        }

        [Fact]
        public async Task TestListNestedOutput()
        {
            var input = new Value
            {
                ListValue = new ListValue
                {
                    Values =
                    {
                        new Value { StringValue = "hello" },
                        CreateOutputValue(ImmutableHashSet<string>.Empty, new Value { StringValue = "world" }),
                    }
                }
            };

            var result = Deserializer.Deserialize(input);
            var v = Assert.IsType<ImmutableArray<object>>(result.Value);
            Assert.Equal("hello", v[0]);
            var o = Assert.IsType<Output<string>>(v[1]);
            var data = await o.DataTask;
            Assert.Equal("world", data.Value);
            Assert.True(data.IsKnown);
            Assert.False(data.IsSecret);
            Assert.Empty(data.Resources);
        }

        [Fact]
        public async Task TestListNestedOutputUnknown()
        {
            var input = new Value
            {
                ListValue = new ListValue
                {
                    Values =
                    {
                        new Value { StringValue = "hello" },
                        CreateOutputValue(ImmutableHashSet<string>.Empty),
                    }
                }
            };

            var result = Deserializer.Deserialize(input);
            var v = Assert.IsType<ImmutableArray<object>>(result.Value);
            Assert.Equal("hello", v[0]);
            var o = Assert.IsType<Output<object?>>(v[1]);
            var data = await o.DataTask;
            Assert.Null(data.Value);
            Assert.False(data.IsKnown);
            Assert.False(data.IsSecret);
            Assert.Empty(data.Resources);
        }

        [Fact]
        public async Task TestStruct()
        {
            var input = CreateOutputValue(ImmutableHashSet<string>.Empty, new Value
            {
                StructValue = new Struct
                {
                    Fields =
                    {
                        { "hello", new Value { StringValue = "world" } },
                        { "foo", new Value { StringValue = "bar" } },
                    }
                }
            });
            var result = Deserializer.Deserialize(input);
            var o = Assert.IsType<Output<ImmutableDictionary<string, object>>>(result.Value);
            var data = await o.DataTask;
            var expected = ImmutableDictionary.Create<string, object>()
                .Add("hello", "world")
                .Add("foo", "bar");
            Assert.Equal(expected, data.Value);
            Assert.True(data.IsKnown);
            Assert.False(data.IsSecret);
            Assert.Empty(data.Resources);
        }

        [Fact]
        public async Task TestStructNestedOutput()
        {
            var input = new Value
            {
                StructValue = new Struct
                {
                    Fields =
                    {
                        { "hello", new Value { StringValue = "world" } },
                        { "foo", CreateOutputValue(ImmutableHashSet<string>.Empty, new Value { StringValue = "bar" }) },
                    }
                }
            };
            var result = Deserializer.Deserialize(input);
            var v = Assert.IsType<ImmutableDictionary<string, object>>(result.Value);
            Assert.Equal("world", v["hello"]);
            var o = Assert.IsType<Output<string>>(v["foo"]);
            var data = await o.DataTask;
            Assert.Equal("bar", data.Value);
            Assert.True(data.IsKnown);
            Assert.False(data.IsSecret);
            Assert.Empty(data.Resources);
        }

        [Fact]
        public async Task TestStructNestedOutputUnknown()
        {
            var input = new Value
            {
                StructValue = new Struct
                {
                    Fields =
                    {
                        { "hello", new Value { StringValue = "world" } },
                        { "foo", CreateOutputValue(ImmutableHashSet<string>.Empty) },
                    }
                }
            };
            var result = Deserializer.Deserialize(input);
            var v = Assert.IsType<ImmutableDictionary<string, object?>>(result.Value);
            Assert.Equal("world", v["hello"]);
            var o = Assert.IsType<Output<object?>>(v["foo"]);
            var data = await o.DataTask;
            Assert.Null(data.Value);
            Assert.False(data.IsKnown);
            Assert.False(data.IsSecret);
            Assert.Empty(data.Resources);
        }
    }
}

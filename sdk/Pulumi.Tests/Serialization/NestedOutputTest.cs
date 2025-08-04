// Copyright 2016-2025, Pulumi Corporation

using System.Collections.Immutable;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Pulumi.Serialization;
using Xunit;

namespace Pulumi.Tests.Serialization
{
    public class NestedOutputTest : ConverterTests
    {
        [OutputType]
        public class NestedOutputType
        {
            [OutputConstructor]
            public NestedOutputType(Output<string> output)
            {
                Output = output;
            }

            public Output<string> Output { get; }
        }

        [OutputType]
        public class DeepNestedOutputType
        {
            [OutputConstructor]
            public DeepNestedOutputType(Output<Output<ImmutableArray<string>>> output)
            {
                Output = output;
            }

            public Output<Output<ImmutableArray<string>>> Output { get; }
        }

        [Fact]
        public async Task NestedCase()
        {
            var stringValue = "Hello World";
            var data = Converter.ConvertValue<NestedOutputType>(NoWarn, "", new Value
            {
                StructValue = new Struct
                {
                    Fields =
                    {
                        {
                            "output", new Value
                            {
                                StringValue = stringValue
                            }
                        },
                    }
                }
            });
            Assert.True(data.IsKnown);
            Assert.Equal(stringValue, await data.Value.Output.GetValueAsync(""));
        }

        [Fact]
        public async Task DeepNestedCase()
        {
            var stringValue = "Hello World";
            var data = Converter.ConvertValue<DeepNestedOutputType>(NoWarn, "", new Value
            {
                StructValue = new Struct
                {
                    Fields =
                    {
                        {
                            "output", new Value
                            {
                                ListValue = new ListValue
                                {
                                    Values =
                                    {
                                        new Value
                                        {
                                            StringValue = stringValue
                                        }
                                    }
                                }
                            }
                        },
                    }
                }
            });
            Assert.True(data.IsKnown);
            var innerOutput = await data.Value.Output.GetValueAsync(Output.Create(ImmutableArray<string>.Empty));
            var listValue = await innerOutput.GetValueAsync(ImmutableArray<string>.Empty);
            Assert.Equivalent(ImmutableArray.Create(stringValue), listValue);
        }

        [Fact]
        public void JsonSerialize()
        {
            var stringValue = "Hello World";
            var data = Converter.ConvertValue<JsonElement>(NoWarn, "", new Value
            {
                StructValue = new Struct
                {
                    Fields =
                    {
                        {
                            "output", new Value
                            {
                                ListValue = new ListValue
                                {
                                    Values =
                                    {
                                        new Value
                                        {
                                            StringValue = stringValue
                                        }
                                    }
                                }
                            }
                        },
                    }
                }
            });
            Assert.True(data.IsKnown);
            Assert.Equal($$"""{"output":["{{stringValue}}"]}""", data.Value.ToString());
        }
    }
}

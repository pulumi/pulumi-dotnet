// Copyright 2016-2024, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Pulumi.Serialization;
using Xunit;

namespace Pulumi.Tests.Serialization
{
    public class ConstructorParamAttributeTests : ConverterTests
    {
        [OutputType]
        public class MyResource
        {
            public readonly string StringProp;

            [OutputConstructor]
            public MyResource([OutputConstructorParameter("string_prop")] string stringProp)
            {
                StringProp = stringProp;
            }
        }

        [Fact]
        public async Task TestOutputConstructorParameter()
        {
            var warnings = new List<string>();

            var data = Converter.ConvertValue<MyResource>(warnings.Add, "", await SerializeToValueAsync(new Dictionary<string, object>
            {
                { "string_prop", "somevalue" },
            }));

            Assert.Equal("somevalue", data.Value.StringProp);
        }
    }
}

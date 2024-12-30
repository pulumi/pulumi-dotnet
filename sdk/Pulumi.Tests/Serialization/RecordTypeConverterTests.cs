using Pulumi.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Pulumi.Tests.Serialization
{
    public class RecordTypeConverterTests : ConverterTests
    {
        [OutputType]
        public record RecordOutput(string Value1, string Value2);

        [Fact]
        public async Task TestRecord()
        {
            var data = Converter.ConvertValue<RecordOutput>(NoWarn, "", await SerializeToValueAsync(new Dictionary<string, object>
            {
                { "Value1", "lorem" },
                { "Value2", "ipsum" }
            }));

            Assert.Equal("lorem", data.Value.Value1);
            Assert.Equal("ipsum", data.Value.Value2);
        }
    }
}

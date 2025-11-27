using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Pulumi.Serialization;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Output<int>))]
[JsonSerializable(typeof(Output<string>))]
[JsonSerializable(typeof(IEnumerable<string>))]
internal partial class PulumiJsonSerializerContext : JsonSerializerContext
{

}

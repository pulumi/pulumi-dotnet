using System.Text.Json.Serialization;

namespace Pulumi.Serialization;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Output<int>))]
[JsonSerializable(typeof(Output<string>))]
internal partial class PulumiJsonSerializerContext : JsonSerializerContext
{

}

using System.Text.Json.Serialization;

namespace Pulumi.Serialization;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Output<int>))]
[JsonSerializable(typeof(Output<string>))]
public partial class PulumiJsonSerializerContext : JsonSerializerContext
{

}

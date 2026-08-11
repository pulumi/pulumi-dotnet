using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pulumi.Automation.Events;

namespace Pulumi.Automation.Serialization.Json;

[JsonSerializable(typeof(ConfigValue))]
[JsonSerializable(typeof(ConfigValueModel))]
[JsonSerializable(typeof(Dictionary<string, ConfigValue>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(EngineEvent))]
[JsonSerializable(typeof(EngineEventModel))]
[JsonSerializable(typeof(IImmutableDictionary<OperationType, int>))]
[JsonSerializable(typeof(IImmutableDictionary<string, ConfigValue>))]
[JsonSerializable(typeof(IImmutableDictionary<string, string>))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(List<PluginInfo>))]
[JsonSerializable(typeof(List<StackSummary>))]
[JsonSerializable(typeof(List<UpdateSummary>))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(object[]))]
[JsonSerializable(typeof(OperationType))]
[JsonSerializable(typeof(PluginInfo))]
[JsonSerializable(typeof(PluginInfoModel))]
[JsonSerializable(typeof(ProjectRuntimeOptions))]
[JsonSerializable(typeof(ProjectSettings))]
[JsonSerializable(typeof(ProjectSettingsModel))]
[JsonSerializable(typeof(StackSettings))]
[JsonSerializable(typeof(StackSummary))]
[JsonSerializable(typeof(StackSummaryModel))]
[JsonSerializable(typeof(UpdateKind))]
[JsonSerializable(typeof(UpdateState))]
[JsonSerializable(typeof(UpdateSummary))]
[JsonSerializable(typeof(UpdateSummaryModel))]
[JsonSerializable(typeof(WhoAmIResult))]
[JsonSourceGenerationOptions(
    Converters = [
        typeof(MapToModelJsonConverter<ConfigValue, ConfigValueModel>),
        typeof(MapToModelJsonConverter<EngineEvent, EngineEventModel>),
        typeof(MapToModelJsonConverter<PluginInfo, PluginInfoModel>),
        typeof(MapToModelJsonConverter<ProjectSettings, ProjectSettingsModel>),
        typeof(MapToModelJsonConverter<StackSummary, StackSummaryModel>),
        typeof(MapToModelJsonConverter<UpdateSummary, UpdateSummaryModel>),
        typeof(ProjectRuntimeJsonConverter),
        typeof(ResourceChangesJsonConverter),
        typeof(StackSettingsConfigValueJsonConverter),
        typeof(StringToEnumJsonConverter<DiffKind, DiffKindConverter>),
        typeof(StringToEnumJsonConverter<OperationType, OperationTypeConverter>),
        typeof(StringToEnumJsonConverter<UpdateKind, UpdateKindConverter>),
        typeof(SystemObjectJsonConverter),
    ],
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    AllowTrailingCommas = true
)]
internal partial class SourceGenerationContext : JsonSerializerContext
{

}

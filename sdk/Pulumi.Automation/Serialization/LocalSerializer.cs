// Copyright 2016-2021, Pulumi Corporation

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pulumi.Automation.Events;
using Pulumi.Automation.Serialization.Json;
using Pulumi.Automation.Serialization.Yaml;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Pulumi.Automation.Serialization
{
    public class LocalSerializer
    {
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IDeserializer _yamlDeserializer;
        private readonly ISerializer _yamlSerializer;

        public LocalSerializer()
        {
            // configure json
            this._jsonOptions = BuildJsonSerializerOptions();

            // configure yaml
            this._yamlDeserializer = BuildYamlDeserializer();
            this._yamlSerializer = BuildYamlSerializer();
        }

        public bool IsValidJson(string content)
        {
            try
            {
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(content));
                reader.Read();
                reader.Skip();
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public T DeserializeJson<T>(string content)
            => JsonSerializer.Deserialize<T>(content, this._jsonOptions)!;

        public T DeserializeYaml<T>(string content)
            where T : class
            => this._yamlDeserializer.Deserialize<T>(content);

        public string SerializeJson<T>(T @object)
            => JsonSerializer.Serialize(@object, this._jsonOptions);

        public string SerializeYaml<T>(T @object)
            where T : class
            => this._yamlSerializer.Serialize(@object);

        public static JsonSerializerOptions BuildJsonSerializerOptions()
        {
            var options = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
#if NETCOREAPP3_1
                IgnoreNullValues = true,
#else
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
#endif
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            options.Converters.Add(new StringToEnumJsonConverter<OperationType, OperationTypeConverter>());
            options.Converters.Add(new StringToEnumJsonConverter<DiffKind, DiffKindConverter>());
            options.Converters.Add(new JsonStringEnumConverter(new LowercaseJsonNamingPolicy()));
            options.Converters.Add(new SystemObjectJsonConverter());
            options.Converters.Add(new MapToModelJsonConverter<ConfigValue, ConfigValueModel>());
            options.Converters.Add(new MapToModelJsonConverter<PluginInfo, PluginInfoModel>());
            options.Converters.Add(new MapToModelJsonConverter<ProjectSettings, ProjectSettingsModel>());
            options.Converters.Add(new MapToModelJsonConverter<StackSummary, StackSummaryModel>());
            options.Converters.Add(new MapToModelJsonConverter<UpdateSummary, UpdateSummaryModel>());
            options.Converters.Add(new MapToModelJsonConverter<EngineEvent, EngineEventModel>());
            options.Converters.Add(new ProjectRuntimeJsonConverter());
            options.Converters.Add(new ResourceChangesJsonConverter());
            options.Converters.Add(new StackSettingsConfigValueJsonConverter());

            return options;
        }

        public static IDeserializer BuildYamlDeserializer()
            => new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .WithTypeConverter(new ProjectRuntimeYamlConverter())
            .WithTypeConverter(new StackSettingsConfigValueYamlConverter())
            .Build();

        public static ISerializer BuildYamlSerializer()
            => new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .WithTypeConverter(new ProjectRuntimeYamlConverter())
            .WithTypeConverter(new StackSettingsConfigValueYamlConverter())
            .Build();
    }
}

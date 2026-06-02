// Copyright 2024, Pulumi Corporation.  All rights reserved.

#nullable enable

using System.Collections.Generic;
using System.Text.Json;
using Pulumi.Esc.Sdk.Client;
using Pulumi.Esc.Sdk.Model;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Pulumi.Esc.Sdk
{
    /// <summary>
    /// Provides serialization and deserialization for <see cref="EnvironmentDefinition"/>.
    /// The ESC API returns environment definitions as <c>application/x-yaml</c> content
    /// from the GetEnvironment, GetEnvironmentAtVersion, and DecryptEnvironment endpoints.
    /// The generated client cannot deserialize YAML, so this class bridges that gap.
    /// </summary>
    public static class EnvironmentDefinitionSerializer
    {
        /// <summary>
        /// Serializes an <see cref="EnvironmentDefinition"/> to JSON.
        /// JSON is valid YAML, so this output can be sent directly to the ESC API
        /// for environment definition updates.
        /// </summary>
        /// <param name="definition">The environment definition to serialize.</param>
        /// <returns>A JSON string representation of the environment definition.</returns>
        public static string Serialize(EnvironmentDefinition definition)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            return JsonSerializer.Serialize(definition, options);
        }

        /// <summary>
        /// Serializes an <see cref="EnvironmentDefinition"/> to YAML.
        /// </summary>
        /// <param name="definition">The environment definition to serialize.</param>
        /// <returns>A YAML string representation of the environment definition.</returns>
        public static string SerializeToYaml(EnvironmentDefinition definition)
        {
            // Convert to an intermediate dictionary, then serialize to YAML
            var dict = new Dictionary<string, object>();

            if (definition.ImportsOption.IsSet && definition.Imports != null)
            {
                dict["imports"] = definition.Imports;
            }

            if (definition.ValuesOption.IsSet && definition.Values != null)
            {
                var valuesDict = new Dictionary<string, object>();
                var values = definition.Values;

                if (values.PulumiConfigOption.IsSet && values.PulumiConfig != null)
                    valuesDict["pulumiConfig"] = values.PulumiConfig;

                if (values.EnvironmentVariablesOption.IsSet && values.EnvironmentVariables != null)
                    valuesDict["environmentVariables"] = values.EnvironmentVariables;

                if (values.FilesOption.IsSet && values.Files != null)
                    valuesDict["files"] = values.Files;

                // Include additional properties (foo, my_secret, my_array, etc.)
                foreach (var kvp in values.AdditionalProperties)
                {
                    valuesDict[kvp.Key] = JsonElementToObject(kvp.Value) ?? "";
                }

                dict["values"] = valuesDict;
            }

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build();

            return serializer.Serialize(dict);
        }

        /// <summary>
        /// Deserializes YAML content returned by the ESC API into an <see cref="EnvironmentDefinition"/>.
        /// This handles the <c>application/x-yaml</c> responses from GetEnvironment,
        /// GetEnvironmentAtVersion, and DecryptEnvironment endpoints.
        /// </summary>
        /// <param name="yaml">The raw YAML string from the API response.</param>
        /// <returns>A parsed <see cref="EnvironmentDefinition"/>, or null if the input is empty.</returns>
        public static EnvironmentDefinition? Deserialize(string? yaml)
        {
            if (string.IsNullOrWhiteSpace(yaml))
                return null;

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var raw = deserializer.Deserialize<Dictionary<string, object?>>(yaml);
            if (raw == null)
                return null;

            // Build the EnvironmentDefinition from the parsed YAML dictionary
            Option<List<string>?> imports = default;
            Option<EnvironmentDefinitionValues?> values = default;

            if (raw.TryGetValue("imports", out var importsObj) && importsObj is List<object> importsList)
            {
                var importStrings = new List<string>();
                foreach (var item in importsList)
                {
                    if (item != null)
                        importStrings.Add(item.ToString()!);
                }
                imports = new Option<List<string>?>(importStrings);
            }

            if (raw.TryGetValue("values", out var valuesObj) && valuesObj is Dictionary<object, object> valuesDict)
            {
                values = new Option<EnvironmentDefinitionValues?>(ParseValues(valuesDict));
            }

            return new EnvironmentDefinition(imports, values);
        }

        private static EnvironmentDefinitionValues ParseValues(Dictionary<object, object> valuesDict)
        {
            Option<Dictionary<string, string>?> envVars = default;
            Option<Dictionary<string, string>?> files = default;
            Option<Dictionary<string, object>?> pulumiConfig = default;

            if (valuesDict.TryGetValue("environmentVariables", out var envVarsObj) && envVarsObj is Dictionary<object, object> envVarsDict)
            {
                envVars = new Option<Dictionary<string, string>?>(ConvertToStringDict(envVarsDict));
            }

            if (valuesDict.TryGetValue("files", out var filesObj) && filesObj is Dictionary<object, object> filesDict)
            {
                files = new Option<Dictionary<string, string>?>(ConvertToStringDict(filesDict));
            }

            if (valuesDict.TryGetValue("pulumiConfig", out var pcObj) && pcObj is Dictionary<object, object> pcDict)
            {
                pulumiConfig = new Option<Dictionary<string, object>?>(ConvertToObjectDict(pcDict));
            }

            var result = new EnvironmentDefinitionValues(envVars, files, pulumiConfig);

            // Capture additional properties (any key not handled above)
            var knownKeys = new HashSet<string> { "environmentVariables", "files", "pulumiConfig" };
            foreach (var kvp in valuesDict)
            {
                var key = kvp.Key.ToString()!;
                if (!knownKeys.Contains(key))
                {
                    result.AdditionalProperties[key] = ObjectToJsonElement(kvp.Value);
                }
            }

            return result;
        }

        private static Dictionary<string, string> ConvertToStringDict(Dictionary<object, object> dict)
        {
            var result = new Dictionary<string, string>();
            foreach (var kvp in dict)
            {
                result[kvp.Key.ToString()!] = kvp.Value?.ToString() ?? "";
            }
            return result;
        }

        private static Dictionary<string, object> ConvertToObjectDict(Dictionary<object, object> dict)
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in dict)
            {
                var key = kvp.Key.ToString()!;
                result[key] = kvp.Value switch
                {
                    Dictionary<object, object> nested => ConvertToObjectDict(nested),
                    List<object> list => list,
                    _ => kvp.Value ?? ""
                };
            }
            return result;
        }

        /// <summary>
        /// Converts a YAML-deserialized object to a <see cref="JsonElement"/>.
        /// This bridges YamlDotNet's object model to System.Text.Json for storing
        /// in <see cref="EnvironmentDefinitionValues.AdditionalProperties"/>.
        /// </summary>
        private static JsonElement ObjectToJsonElement(object? value)
        {
            // Serialize the object to JSON then parse back as JsonElement
            var json = JsonSerializer.Serialize(ConvertYamlValue(value));
            return JsonDocument.Parse(json).RootElement.Clone();
        }

        /// <summary>
        /// Converts a <see cref="JsonElement"/> to a plain .NET object suitable for
        /// YamlDotNet serialization.
        /// </summary>
        private static object? JsonElementToObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object?>();
                    foreach (var prop in element.EnumerateObject())
                        dict[prop.Name] = JsonElementToObject(prop.Value);
                    return dict;
                case JsonValueKind.Array:
                    var list = new List<object?>();
                    foreach (var item in element.EnumerateArray())
                        list.Add(JsonElementToObject(item));
                    return list;
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    // Preserve integer vs floating point
                    if (element.TryGetInt64(out var longVal))
                        return longVal;
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Converts YamlDotNet's object types to standard .NET types suitable for
        /// JSON serialization. YamlDotNet uses Dictionary&lt;object, object&gt; for
        /// mappings and List&lt;object&gt; for sequences.
        /// </summary>
        private static object? ConvertYamlValue(object? value)
        {
            return value switch
            {
                Dictionary<object, object> dict => ConvertYamlDict(dict),
                List<object> list => ConvertYamlList(list),
                _ => value
            };
        }

        private static Dictionary<string, object?> ConvertYamlDict(Dictionary<object, object> dict)
        {
            var result = new Dictionary<string, object?>();
            foreach (var kvp in dict)
                result[kvp.Key.ToString()!] = ConvertYamlValue(kvp.Value);
            return result;
        }

        private static List<object?> ConvertYamlList(List<object> list)
        {
            var result = new List<object?>();
            foreach (var item in list)
                result.Add(ConvertYamlValue(item));
            return result;
        }
    }
}

using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Linq;

namespace Pulumi.Experimental.Provider
{
    public static class BuiltinTypeSpec
    {
        public const string String = "string";
        public const string Integer = "integer";
        public const string Number = "number";
        public const string Boolean = "boolean";
        public const string Object = "object";
    }

    public class ItemTypeSpec
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
    }

    public class PropertySpec
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("willReplaceOnChanges")]
        public bool? WillReplaceOnChanges { get; set; }

        [JsonPropertyName("items")]
        public ItemTypeSpec? Items { get; set; }

        [JsonPropertyName("$ref")]
        public string? Ref { get; set; }

        public static PropertySpec FromDefinition(PropertyDefinition property)
        {
            return new PropertySpec
            {
                Description = property.Description,
                Type = property.Type,
                WillReplaceOnChanges = false,
                Items = null,
                Ref = property.Ref
            };
        }
    }

    public class ComplexTypeSpec
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("properties")]
        public Dictionary<string, PropertySpec> Properties { get; set; } = new();

        [JsonPropertyName("required")]
        public List<string> Required { get; set; } = new();

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("enum")]
        public List<object>? Enum { get; set; }

        public static ComplexTypeSpec FromDefinition(TypeDefinition typeDef)
        {
            return new ComplexTypeSpec
            {
                Type = "object",
                Properties = typeDef.Properties.ToDictionary(
                    kvp => kvp.Key,
                    kvp => PropertySpec.FromDefinition(kvp.Value)),
                Required = new List<string>(),
                Description = typeDef.Description
            };
        }
    }

    public class ResourceSpec
    {
        [JsonPropertyName("isComponent")]
        public bool IsComponent { get; set; }

        [JsonPropertyName("inputProperties")]
        public Dictionary<string, PropertySpec> InputProperties { get; set; } = new();

        [JsonPropertyName("requiredInputs")]
        public List<string> RequiredInputs { get; set; } = new();

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("properties")]
        public Dictionary<string, PropertySpec> Properties { get; set; } = new();

        [JsonPropertyName("required")]
        public List<string> Required { get; set; } = new();

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        public static ResourceSpec FromDefinition(ComponentDefinition component)
        {
            return new ResourceSpec
            {
                IsComponent = true,
                Type = "object",
                InputProperties = component.Inputs.ToDictionary(
                    kvp => kvp.Key,
                    kvp => PropertySpec.FromDefinition(kvp.Value)),
                RequiredInputs = component.Inputs
                    .Where(kvp => !kvp.Value.Optional)
                    .Select(kvp => kvp.Key)
                    .ToList(),
                Properties = component.Outputs.ToDictionary(
                    kvp => kvp.Key,
                    kvp => PropertySpec.FromDefinition(kvp.Value)),
                Required = component.Outputs
                    .Where(kvp => !kvp.Value.Optional)
                    .Select(kvp => kvp.Key)
                    .ToList()
            };
        }
    }

    public class PackageSpec
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "";

        [JsonPropertyName("resources")]
        public Dictionary<string, ResourceSpec> Resources { get; set; } = new();

        [JsonPropertyName("types")]
        public Dictionary<string, ComplexTypeSpec> Types { get; set; } = new();

        [JsonPropertyName("language")]
        public Dictionary<string, Dictionary<string, object>> Language { get; set; } = new();

        public static PackageSpec GenerateSchema(
            Metadata metadata,
            Dictionary<string, ComponentDefinition> components,
            Dictionary<string, TypeDefinition> typeDefinitions)
        {
            var pkg = new PackageSpec
            {
                Name = metadata.Name,
                Version = metadata.Version,
                DisplayName = metadata.DisplayName ?? metadata.Name,
                Language = new Dictionary<string, Dictionary<string, object>>
                {
                    ["nodejs"] = new() { ["respectSchemaVersion"] = true },
                    ["python"] = new() { ["respectSchemaVersion"] = true },
                    ["csharp"] = new() { ["respectSchemaVersion"] = true },
                    ["java"] = new() { ["respectSchemaVersion"] = true },
                    ["go"] = new() { ["respectSchemaVersion"] = true }
                }
            };

            foreach (var (componentName, component) in components)
            {
                var name = $"{metadata.Name}:index:{componentName}";
                pkg.Resources[name] = ResourceSpec.FromDefinition(component);
            }

            foreach (var (typeName, type) in typeDefinitions)
            {
                pkg.Types[$"{metadata.Name}:index:{typeName}"] = ComplexTypeSpec.FromDefinition(type);
            }

            return pkg;
        }
    }
}

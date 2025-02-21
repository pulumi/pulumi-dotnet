// Copyright 2025, Pulumi Corporation

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Pulumi.Experimental.Provider
{
    public record PackageSpec
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("displayName")]
        public string DisplayName { get; init; } = "";

        [JsonPropertyName("version")]
        public string Version { get; init; } = "";

        [JsonPropertyName("resources")]
        public ImmutableSortedDictionary<string, ResourceSpec> Resources { get; init; } =
            ImmutableSortedDictionary<string, ResourceSpec>.Empty;

        [JsonPropertyName("types")]
        public ImmutableSortedDictionary<string, ComplexTypeSpec> Types { get; init; } =
            ImmutableSortedDictionary<string, ComplexTypeSpec>.Empty;

        [JsonPropertyName("language")]
        public ImmutableSortedDictionary<string, ImmutableSortedDictionary<string, object>> Language { get; init; } =
            ImmutableSortedDictionary<string, ImmutableSortedDictionary<string, object>>.Empty;
    }

    public record ResourceSpec : ObjectTypeSpec
    {
        [JsonPropertyName("isComponent")]
        public bool IsComponent { get; init; }

        [JsonPropertyName("inputProperties")]
        public ImmutableSortedDictionary<string, PropertySpec> InputProperties { get; init; } =
            ImmutableSortedDictionary<string, PropertySpec>.Empty;

        [JsonPropertyName("requiredInputs")]
        public ImmutableSortedSet<string> RequiredInputs { get; init; } =
            ImmutableSortedSet<string>.Empty;

        public ResourceSpec(
            Dictionary<string, PropertySpec> inputProperties,
            HashSet<string> requiredInputs,
            Dictionary<string, PropertySpec> properties,
            HashSet<string> required)
        {
            IsComponent = true;
            Type = "object";
            InputProperties = inputProperties.ToImmutableSortedDictionary();
            RequiredInputs = requiredInputs.ToImmutableSortedSet();
            Properties = properties.ToImmutableSortedDictionary();
            Required = required.ToImmutableSortedSet();
        }
    }

    public record TypeSpec
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("items")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TypeSpec? Items { get; init; }

        [JsonPropertyName("additionalProperties")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TypeSpec? AdditionalProperties { get; init; }

        [JsonPropertyName("$ref")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Ref { get; init; }

        [JsonPropertyName("plain")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool? Plain { get; init; }

        public static TypeSpec CreateBuiltin(string type, bool? plain = null) =>
            new() { Type = type, Plain = plain == true ? true : null };

        public static TypeSpec CreateReference(string reference, bool? plain = null) =>
            new() { Ref = reference, Plain = plain == true ? true : null };

        public static TypeSpec CreateArray(TypeSpec items) =>
            new() { Type = "array", Items = items };

        public static TypeSpec CreateDictionary(TypeSpec additionalProperties) =>
            new() { Type = "object", AdditionalProperties = additionalProperties };
    }

    public record PropertySpec : TypeSpec
    {
        public static PropertySpec String => CreateBuiltin(BuiltinTypeSpec.String);
        public static PropertySpec Integer => CreateBuiltin(BuiltinTypeSpec.Integer);
        public static PropertySpec Number => CreateBuiltin(BuiltinTypeSpec.Number);
        public static PropertySpec Boolean => CreateBuiltin(BuiltinTypeSpec.Boolean);

        public new static PropertySpec CreateBuiltin(string type, bool? plain = null) =>
            new() { Type = type, Plain = plain == true ? true : null };

        public new static PropertySpec CreateReference(string reference, bool? plain = null) =>
            new() { Ref = reference, Plain = plain == true ? true : null };

        public new static PropertySpec CreateArray(TypeSpec items) =>
            new() { Type = "array", Items = items };

        public new static PropertySpec CreateDictionary(TypeSpec additionalProperties) =>
            new() { Type = "object", AdditionalProperties = additionalProperties };
    }

    public record ObjectTypeSpec
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "";

        [JsonPropertyName("properties")]
        public ImmutableSortedDictionary<string, PropertySpec> Properties { get; init; } =
            ImmutableSortedDictionary<string, PropertySpec>.Empty;

        [JsonPropertyName("required")]
        public ImmutableSortedSet<string> Required { get; init; } =
            ImmutableSortedSet<string>.Empty;
    }

    public record ComplexTypeSpec : ObjectTypeSpec
    {
        private ComplexTypeSpec(string type)
        {
            Type = type;
        }

        public static ComplexTypeSpec CreateObject(
            Dictionary<string, PropertySpec> properties,
            HashSet<string> required) =>
            new("object")
            {
                Properties = properties.ToImmutableSortedDictionary(),
                Required = required.ToImmutableSortedSet()
            };
    }

    public static class BuiltinTypeSpec
    {
        public const string String = "string";
        public const string Integer = "integer";
        public const string Number = "number";
        public const string Boolean = "boolean";
        public const string Object = "object";
    }
}

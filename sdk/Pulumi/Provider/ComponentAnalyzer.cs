// Copyright 2025, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
namespace Pulumi.Experimental.Provider
{
    /// <summary>
    /// Analyzes component resource types and generates a package schema.
    /// </summary>
    public sealed class ComponentAnalyzer
    {
        private readonly Metadata metadata;
        private readonly Dictionary<string, ComplexTypeSpec> typeDefinitions = new();

        private ComponentAnalyzer(Metadata metadata)
        {
            this.metadata = metadata;
        }

        /// <summary>
        /// Analyzes the components in the given assembly and generates a package schema.
        /// </summary>
        /// <param name="metadata">The package metadata including name (required), version and display name (optional)</param>
        /// <param name="assembly">The assembly containing component resource types to analyze</param>
        /// <returns>A PackageSpec containing the complete schema for all components and their types</returns>
        public static PackageSpec GenerateSchema(Metadata metadata, Assembly assembly)
        {
            var types = assembly.GetTypes()
                .Where(t => typeof(ComponentResource).IsAssignableFrom(t) && !t.IsAbstract);
            return GenerateSchema(metadata, types.ToArray());
        }

        /// <summary>
        /// Analyzes the specified component types and generates a package schema.
        /// </summary>
        /// <param name="metadata">The package metadata including name (required), version and display name (optional)</param>
        /// <param name="componentTypes">The component resource types to analyze</param>
        /// <returns>A PackageSpec containing the complete schema for all components and their types</returns>
        public static PackageSpec GenerateSchema(Metadata metadata, params Type[] componentTypes)
        {
            if (metadata?.Name == null || string.IsNullOrWhiteSpace(metadata.Name))
                throw new ArgumentException("Package name cannot be empty or whitespace", nameof(metadata));

            if (componentTypes.Length == 0)
            {
                throw new ArgumentException("At least one component type must be provided");
            }

            var analyzer = new ComponentAnalyzer(metadata);
            var components = new Dictionary<string, ResourceSpec>();

            foreach (var type in componentTypes)
            {
                if (!typeof(ComponentResource).IsAssignableFrom(type))
                {
                    throw new ArgumentException($"Type {type.Name} must inherit from ComponentResource");
                }
                components[type.Name] = analyzer.AnalyzeComponent(type);
            }

            return analyzer.GenerateSchema(metadata, components, analyzer.typeDefinitions);
        }

        private PackageSpec GenerateSchema(
            Metadata metadata,
            Dictionary<string, ResourceSpec> components,
            Dictionary<string, ComplexTypeSpec> typeDefinitions)
        {
            var languages = new Dictionary<string, ImmutableSortedDictionary<string, object>>();
            var settings = new Dictionary<string, object>
            {
                ["respectSchemaVersion"] = true
            };
            foreach (var lang in new[] { "nodejs", "python", "csharp", "java", "go" })
            {
                languages.Add(lang, ImmutableSortedDictionary.CreateRange(settings));
            }

            var resources = new Dictionary<string, ResourceSpec>();
            foreach (var (componentName, component) in components)
            {
                var name = $"{metadata.Name}:index:{componentName}";
                resources.Add(name, component);
            }

            var types = new Dictionary<string, ComplexTypeSpec>();
            foreach (var (typeName, type) in typeDefinitions)
            {
                types.Add($"{metadata.Name}:index:{typeName}", type);
            }

            return new PackageSpec
            {
                Name = metadata.Name,
                Version = metadata.Version ?? "",
                DisplayName = metadata.DisplayName ?? metadata.Name,
                Language = languages.ToImmutableSortedDictionary(),
                Resources = resources.ToImmutableSortedDictionary(),
                Types = types.ToImmutableSortedDictionary()
            };
        }

        private ResourceSpec AnalyzeComponent(Type componentType)
        {
            var argsType = GetArgsType(componentType);
            var inputAnalysis = AnalyzeType(argsType);
            var outputAnalysis = AnalyzeType(componentType);

            return new ResourceSpec(
                inputAnalysis.Properties,
                inputAnalysis.Required,
                outputAnalysis.Properties,
                outputAnalysis.Required);
        }

        private Type GetArgsType(Type componentType)
        {
            return componentType.GetConstructors()
                .Where(c => c.GetParameters().Length == 3)  // Exactly 3 parameters
                .Where(c => typeof(ResourceArgs).IsAssignableFrom(c.GetParameters()[1].ParameterType))
                .Where(c => typeof(ComponentResourceOptions).IsAssignableFrom(c.GetParameters()[2].ParameterType))  // Third parameter must be ComponentResourceOptions
                .Select(c => c.GetParameters()[1].ParameterType)
                .FirstOrDefault()
                ?? throw new ArgumentException(
                    $"Component {componentType.Name} must have a constructor with exactly three parameters: " +
                    "a string name, a parameter that extends ResourceArgs, and ComponentResourceOptions");
        }

        private record TypeAnalysis(
            Dictionary<string, PropertySpec> Properties,
            HashSet<string> Required);

        private TypeAnalysis AnalyzeType(Type type)
        {
            var properties = new Dictionary<string, PropertySpec>();
            var required = new HashSet<string>();

            // Analyze both fields and properties
            var members = type.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m is FieldInfo or PropertyInfo);

            foreach (var member in members)
            {
                var schemaName = GetSchemaPropertyName(member);
                properties[schemaName] = AnalyzeProperty(member);
                if (!IsOptionalProperty(member))
                {
                    required.Add(schemaName);
                }
            }

            return new TypeAnalysis(properties, required);
        }

        private PropertySpec AnalyzeProperty(MemberInfo member)
        {
            Type memberType = member switch
            {
                PropertyInfo prop => prop.PropertyType,
                FieldInfo field => field.FieldType,
                _ => throw new ArgumentException($"Unsupported member type: {member.GetType()}")
            };

            // Check if this is an input or output property
            var isOutput = member.GetCustomAttribute<OutputAttribute>() != null;

            var typeSpec = AnalyzeTypeParameter(memberType, $"{member.DeclaringType?.Name}.{member.Name}", isOutput);
            return new PropertySpec
            {
                Type = typeSpec.Type,
                Ref = typeSpec.Ref,
                Plain = typeSpec.Plain,
                Items = typeSpec.Items,
                AdditionalProperties = typeSpec.AdditionalProperties
            };
        }

        private bool IsOptionalProperty(MemberInfo member)
        {
            Type memberType = member switch
            {
                PropertyInfo prop => prop.PropertyType,
                FieldInfo field => field.FieldType,
                _ => throw new ArgumentException($"Unsupported member type: {member.GetType()}")
            };

            // Inputs have an explicit annotation for requiredness
            var inputAttr = member.GetCustomAttribute<InputAttribute>();
            if (inputAttr != null)
            {
                return !inputAttr.IsRequired;
            }

            // For Output<T>, check if T is nullable
            if (memberType.IsGenericType && memberType.GetGenericTypeDefinition() == typeof(Output<>))
            {
                var outputType = memberType.GetGenericArguments()[0];

                // Check if T is a nullable value type (Nullable<T>)
                if (outputType.IsGenericType && outputType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    return true;
                }

                // For reference types, check if it's nullable
                if (!outputType.IsValueType)
                {
                    var nullableAttribute = member.CustomAttributes
                        .FirstOrDefault(attr => attr.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
                    return nullableAttribute != null;
                }

                // For non-nullable value types in Output<T>, they are required
                return false;
            }

            return true;
        }

        private string GetSchemaPropertyName(MemberInfo member)
        {
            var inputAttr = member.GetCustomAttribute<InputAttribute>();
            if (inputAttr != null && !string.IsNullOrEmpty(inputAttr.Name))
            {
                return inputAttr.Name;
            }

            var outputAttr = member.GetCustomAttribute<OutputAttribute>();
            if (outputAttr != null && !string.IsNullOrEmpty(outputAttr.Name))
            {
                return outputAttr.Name;
            }

            throw new ArgumentException($"Property {member.Name} has no Input or Output attribute");
        }

        private TypeSpec AnalyzeTypeParameter(Type type, string context, bool isOutput)
        {
            // Strings, numbers, etc.
            var builtinType = GetBuiltinTypeName(type);
            if (builtinType != null)
            {
                return TypeSpec.CreateBuiltin(builtinType, !isOutput);
            }

            // Special types like Archive, Asset, etc.
            var specialTypeRef = GetSpecialTypeRef(type);
            if (specialTypeRef != null)
            {
                return TypeSpec.CreateReference(specialTypeRef, !isOutput);
            }

            // Resource references are not supported yet
            if (typeof(CustomResource).IsAssignableFrom(type))
            {
                throw new ArgumentException(
                    $"Resource references are not supported yet: found type '{type.Name}' for '{context}'");
            }

            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                var itemSpec = AnalyzeTypeParameter(elementType, context, isOutput);
                return TypeSpec.CreateArray(itemSpec);
            }

            if (type.IsGenericType)
            {
                var genericTypeDef = type.GetGenericTypeDefinition();
                if (genericTypeDef == typeof(Output<>))
                {
                    if (!isOutput)
                    {
                        throw new ArgumentException($"Output<T> can only be used for output properties: {context}");
                    }
                    return AnalyzeTypeParameter(type.GetGenericArguments()[0], context, true);
                }
                if (genericTypeDef == typeof(Input<>))
                {
                    if (isOutput)
                    {
                        throw new ArgumentException($"Input<T> can only be used for input properties: {context}");
                    }
                    var typeSpec = AnalyzeTypeParameter(type.GetGenericArguments()[0], context, false);
                    return typeSpec with { Plain = null };
                }
                if (genericTypeDef == typeof(InputMap<>))
                {
                    if (isOutput)
                    {
                        throw new ArgumentException($"InputMap<T> can only be used for input properties: {context}");
                    }
                    var valueType = type.GetGenericArguments()[0];
                    var valueSpec = AnalyzeTypeParameter(valueType, context, false);
                    return TypeSpec.CreateDictionary(valueSpec with { Plain = null });
                }
                if (genericTypeDef == typeof(InputList<>))
                {
                    if (isOutput)
                    {
                        throw new ArgumentException($"InputList<T> can only be used for input properties: {context}");
                    }
                    var itemType = type.GetGenericArguments()[0];
                    var itemSpec = AnalyzeTypeParameter(itemType, context, false);
                    return TypeSpec.CreateArray(itemSpec with { Plain = null });
                }
                if (genericTypeDef == typeof(Nullable<>))
                {
                    // For nullable value types, analyze the underlying type
                    return AnalyzeTypeParameter(type.GetGenericArguments()[0], context, isOutput);
                }
                if (genericTypeDef == typeof(List<>) || genericTypeDef == typeof(IList<>))
                {
                    var itemSpec = AnalyzeTypeParameter(type.GetGenericArguments()[0], context, isOutput);
                    return TypeSpec.CreateArray(itemSpec);
                }
                if (genericTypeDef == typeof(Dictionary<,>) || genericTypeDef == typeof(IDictionary<,>))
                {
                    var keyType = type.GetGenericArguments()[0];
                    if (keyType != typeof(string))
                    {
                        throw new ArgumentException(
                            $"Dictionary keys must be strings, got '{keyType.Name}' for '{context}'");
                    }
                    var valueSpec = AnalyzeTypeParameter(type.GetGenericArguments()[1], context, isOutput);
                    return TypeSpec.CreateDictionary(valueSpec);
                }
            }

            if (!type.IsInterface && !type.IsPrimitive && type != typeof(string) && !(type.Namespace?.StartsWith("System") ?? false))
            {
                var typeName = GetTypeName(type);
                var typeRef = $"#/types/{metadata.Name}:index:{typeName}";

                if (!typeDefinitions.ContainsKey(typeName))
                {
                    typeDefinitions[typeName] = ComplexTypeSpec.CreateObject(new(), new());
                    var analysis = AnalyzeType(type);
                    typeDefinitions[typeName] = ComplexTypeSpec.CreateObject(analysis.Properties, analysis.Required);
                }

                return TypeSpec.CreateReference(typeRef, !isOutput);
            }

            throw new ArgumentException($"Type '{type.FullName}' is not supported as a parameter type");
        }

        private string GetTypeName(Type type)
        {
            var name = type.Name;
            return name.EndsWith("Args") ? name[..^4] : name;
        }

        private string? GetBuiltinTypeName(Type type)
        {
            if (type == typeof(string))
                return BuiltinTypeSpec.String;
            if (type == typeof(int) || type == typeof(long))
                return BuiltinTypeSpec.Integer;
            if (type == typeof(double) || type == typeof(float))
                return BuiltinTypeSpec.Number;
            if (type == typeof(bool))
                return BuiltinTypeSpec.Boolean;
            return null;
        }

        private string? GetSpecialTypeRef(Type type)
        {
            if (type == typeof(Archive))
                return "pulumi.json#/Archive";
            if (type == typeof(Asset))
                return "pulumi.json#/Asset";
            return null;
        }
    }

    public class Metadata
    {
        public string Name { get; }
        public string? Version { get; }
        public string? DisplayName { get; }

        public Metadata(string name, string? version = null, string? displayName = null)
        {
            Name = name;
            Version = version;
            DisplayName = displayName;
        }
    }
}

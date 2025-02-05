using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace Pulumi.Experimental.Provider
{
    public class Analyzer
    {
        private readonly Metadata metadata;
        private readonly Dictionary<string, TypeDefinition> typeDefinitions = new();

        public Analyzer(Metadata metadata)
        {
            this.metadata = metadata;
        }

        public (Dictionary<string, ComponentDefinition>, Dictionary<string, TypeDefinition>) Analyze(Assembly assembly)
        {
            var components = new Dictionary<string, ComponentDefinition>();
            
            // Find all component resources in the assembly
            var types = assembly.GetTypes();
            foreach (var type in types)
            {
                if (typeof(ComponentResource).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    components[type.Name] = AnalyzeComponent(type);
                }
            }

            return (components, typeDefinitions);
        }

        private ComponentDefinition AnalyzeComponent(Type componentType)
        {
            // Get constructor args type
            var argsType = GetArgsType(componentType);
            var (inputs, inputsMapping) = AnalyzeType(argsType);
            var (outputs, outputsMapping) = AnalyzeOutputs(componentType);

            return new ComponentDefinition
            {
                Description = componentType.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description,
                Inputs = inputs,
                InputsMapping = inputsMapping,
                Outputs = outputs,
                OutputsMapping = outputsMapping
            };
        }

        private static Type GetArgsType(Type componentType)
        {
            var constructor = componentType.GetConstructors().First();
            var argsParameter = constructor.GetParameters()
                .FirstOrDefault(p => p.Name == "args")
                ?? throw new ArgumentException($"Component {componentType.Name} must have an 'args' parameter in constructor");
            
            return argsParameter.ParameterType;
        }

        private (Dictionary<string, PropertyDefinition>, Dictionary<string, string>) AnalyzeType(Type type)
        {
            var properties = new Dictionary<string, PropertyDefinition>();
            var mapping = new Dictionary<string, string>();

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var schemaName = GetSchemaPropertyName(prop);
                mapping[schemaName] = prop.Name;

                var propertyDef = AnalyzeProperty(prop);
                if (propertyDef != null)
                {
                    properties[schemaName] = propertyDef;
                }
            }

            return (properties, mapping);
        }

        private (Dictionary<string, PropertyDefinition>, Dictionary<string, string>) AnalyzeOutputs(Type type)
        {
            var properties = new Dictionary<string, PropertyDefinition>();
            var mapping = new Dictionary<string, string>();

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // Only look at properties marked with [Output]
                var outputAttr = prop.GetCustomAttribute<OutputAttribute>();
                if (outputAttr == null) continue;

                var schemaName = outputAttr.Name ?? GetSchemaPropertyName(prop);
                mapping[schemaName] = prop.Name;

                var propertyDef = AnalyzeProperty(prop);
                if (propertyDef != null)
                {
                    properties[schemaName] = propertyDef;
                }
            }

            return (properties, mapping);
        }

        private PropertyDefinition? AnalyzeProperty(PropertyInfo prop)
        {
            var propType = prop.PropertyType;
            var isOptional = IsOptionalProperty(prop);

            // Handle Input<T> and Output<T>
            if (IsInputOrOutput(propType, out var unwrappedType))
            {
                propType = unwrappedType;
            }

            // Get property description
            var description = prop.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description;

            if (IsBuiltinType(propType, out var builtinType))
            {
                return new PropertyDefinition
                {
                    Description = description,
                    Type = builtinType,
                    Optional = isOptional
                };
            }
            else if (propType.IsClass && propType != typeof(string))
            {
                // Complex type
                var typeName = propType.Name;
                var typeRef = $"#/types/{metadata.Name}:index:{typeName}";

                // Add to type definitions if not already present
                if (!typeDefinitions.ContainsKey(typeName))
                {
                    var (properties, mapping) = AnalyzeType(propType);
                    typeDefinitions[typeName] = new TypeDefinition
                    {
                        Name = typeName,
                        Description = propType.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description,
                        Properties = properties,
                        PropertiesMapping = mapping
                    };
                }

                return new PropertyDefinition
                {
                    Description = description,
                    Ref = typeRef,
                    Optional = isOptional
                };
            }

            return null;
        }

        private static bool IsInputOrOutput(Type type, out Type unwrappedType)
        {
            unwrappedType = type;

            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                if (genericDef == typeof(Input<>) || genericDef == typeof(Output<>))
                {
                    unwrappedType = type.GetGenericArguments()[0];
                    return true;
                }
            }

            return false;
        }

        private static bool IsBuiltinType(Type type, out string builtinType)
        {
            if (type == typeof(string))
            {
                builtinType = BuiltinTypeSpec.String;
                return true;
            }
            if (type == typeof(int) || type == typeof(long))
            {
                builtinType = BuiltinTypeSpec.Integer;
                return true;
            }
            if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
            {
                builtinType = BuiltinTypeSpec.Number;
                return true;
            }
            if (type == typeof(bool))
            {
                builtinType = BuiltinTypeSpec.Boolean;
                return true;
            }

            builtinType = BuiltinTypeSpec.Object;
            return false;
        }

        private static bool IsOptionalProperty(PropertyInfo prop)
        {
            // Check if type is nullable
            if (Nullable.GetUnderlyingType(prop.PropertyType) != null)
                return true;

            // Check if property has [Input] attribute with required=false
            var inputAttr = prop.GetCustomAttribute<InputAttribute>();
            if (inputAttr != null)
                return !inputAttr.IsRequired;

            // Default to optional
            return true;
        }

        private static string GetSchemaPropertyName(PropertyInfo prop)
        {
            // Check for explicit name in Input attribute
            var inputAttr = prop.GetCustomAttribute<InputAttribute>();
            if (inputAttr != null && !string.IsNullOrEmpty(inputAttr.Name))
                return inputAttr.Name;

            // Convert to camelCase
            var name = prop.Name;
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json.Linq;

namespace Pulumi.Experimental.Provider
{
    internal class ComponentMetadata
    {
        internal class TypeReference
        {
            public readonly string FullReference;
            public readonly string Token;
            public readonly bool IsExternal;

            public TypeReference(string fullReference, string token, bool isExternal)
            {
                FullReference = fullReference;
                Token = token;
                IsExternal = isExternal;
            }
        }

        public readonly Type? ArgsType;
        public readonly Type ResourceType;
        public readonly Dictionary<Type, TypeReference> TypeReferences = new Dictionary<Type, TypeReference>();
        private readonly string _providerName;

        private void ComputeTypeReferences(Type argsType)
        {
            if (argsType.IsArray)
            {
                var elementType = argsType.GetElementType()!;
                ComputeTypeReferences(elementType);
            }
            else if (argsType.IsGenericType)
            {
                if (argsType.GetGenericTypeDefinition() == typeof(Input<>))
                {
                    var elementType = argsType.GetGenericArguments()[0];
                    ComputeTypeReferences(elementType);
                }
                else if (argsType.GetGenericTypeDefinition() == typeof(InputList<>))
                {
                    var elementType = argsType.GetGenericArguments()[0];
                    ComputeTypeReferences(elementType);
                }
                else if (argsType.GetGenericTypeDefinition() == typeof(InputMap<>))
                {
                    var elementType = argsType.GetGenericArguments()[0];
                    ComputeTypeReferences(elementType);
                }
                else if (argsType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    var elementType = argsType.GetGenericArguments()[0];
                    ComputeTypeReferences(elementType);
                }
                else if (argsType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    var valueType = argsType.GetGenericArguments()[1];
                    ComputeTypeReferences(valueType);
                }
                else if (argsType.GetGenericTypeDefinition() == typeof(ImmutableDictionary<,>))
                {
                    var valueType = argsType.GetGenericArguments()[1];
                    ComputeTypeReferences(valueType);
                }
                else if (argsType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var elementType = argsType.GetGenericArguments()[0];
                    ComputeTypeReferences(elementType);
                }
                else if (argsType.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
                {
                    var elementType = argsType.GetGenericArguments()[0];
                    ComputeTypeReferences(elementType);
                }
            }
            else
            {
                var fullName = argsType.FullName ?? "";
                var userAssemblyFullName = ResourceType.Assembly.FullName ?? "";
                var isUserDefinedType = !fullName.StartsWith("System.");
                if (argsType.IsClass && isUserDefinedType && argsType.Assembly.FullName == userAssemblyFullName)
                {
                    // for classes defined by the user, to be encoded within "types" schema section
                    var typeToken = $"{_providerName}::{argsType.Name}";
                    var typeReference = $"#/types/{typeToken}";
                    TypeReferences[argsType] = new TypeReference(
                        fullReference: typeReference,
                        token: typeToken,
                        isExternal: false);

                    foreach (var property in argsType.GetProperties())
                    {
                        ComputeTypeReferences(property.PropertyType);
                    }
                }
                else if (isUserDefinedType && argsType.IsEnum)
                {
                    var typeToken = $"{_providerName}::{argsType.Name}";
                    var typeReference = $"#/types/{typeToken}";
                    TypeReferences[argsType] = new TypeReference(
                        fullReference: typeReference,
                        token: typeToken,
                        isExternal: false);
                }
                else if (isUserDefinedType)
                {
                    // other types from external schemas
                    // TODO: support external schemas
                }
            }
        }

        public ComponentMetadata(string provider, Type argsType, Type resourceType)
        {
            ArgsType = argsType;
            ResourceType = resourceType;
            _providerName = provider;
            ComputeTypeReferences(argsType);
        }

        public ComponentMetadata(string provider, Type resourceType)
        {
            ArgsType = null;
            ResourceType = resourceType;
            _providerName = provider;
        }
    }

    public class ComponentProviderBuilder
    {
        internal readonly Dictionary<string, Func<ConstructRequest, object, ComponentResource>> ResourceConstructors = new Dictionary<string, Func<ConstructRequest, object, ComponentResource>>();
        internal readonly Dictionary<string, ComponentMetadata> ResourceMetadata = new Dictionary<string, ComponentMetadata>();
        public string ProviderName { get; set; } = "provider";
        public string ProviderVersion { get; set; } = "0.1.0";
        private Func<JObject, JObject> _extendSchema = schema => schema;
        public ComponentProviderBuilder RegisterComponent<T, U>(string type, Func<ConstructRequest, T, U> constructor)
            where T : ResourceArgs
            where U : ComponentResource
        {
            ResourceConstructors[type] = (request, args) => constructor(request, (T)args);
            ResourceMetadata[type] = new ComponentMetadata(ProviderName, argsType: typeof(T), resourceType: typeof(U));
            return this;
        }

        public ComponentProviderBuilder RegisterComponent<T>(string type, Func<ConstructRequest, T> constructor)
            where T : ComponentResource
        {
            ResourceConstructors[type] = (request, args) => constructor(request);
            ResourceMetadata[type] = new ComponentMetadata(ProviderName, resourceType: typeof(T));
            return this;
        }

        public ComponentProviderBuilder Name(string providerName)
        {
            ProviderName = providerName;
            return this;
        }

        public ComponentProviderBuilder Version(string version)
        {
            ProviderVersion = version;
            return this;
        }

        public ComponentProviderBuilder ExtendSchema(Func<JObject, JObject> extendSchema)
        {
            _extendSchema = extendSchema;
            return this;
        }

        internal static readonly object XmlCacheLock = new object();
        internal static readonly Dictionary<string, Assembly> LoadedAssemblies = new Dictionary<string, Assembly>();
        internal static readonly Dictionary<string, string> LoadedXmlDocumentation = new Dictionary<string, string>();
        public static string? GetDirectoryPath(Assembly assembly)
        {
            string? directoryPath = Path.GetDirectoryName(assembly.Location);
            return directoryPath == string.Empty ? null : directoryPath;
        }

        internal static bool LoadXmlDocumentation(Assembly assembly)
        {
            if (LoadedAssemblies.ContainsKey(assembly.FullName ?? ""))
            {
                return false;
            }

            bool newContent = false;
            string? directoryPath = GetDirectoryPath(assembly);
            if (directoryPath != null)
            {
                string xmlFilePath = Path.Combine(directoryPath, assembly.GetName().Name + ".xml");
                if (File.Exists(xmlFilePath))
                {
                    using StreamReader streamReader = new StreamReader(xmlFilePath);
                    LoadXmlDocumentationNoLock(assembly, streamReader);
                    newContent = true;
                }
            }
            LoadedAssemblies.Add(assembly.FullName ?? "", assembly);
            return newContent;
        }

        internal static void LoadXmlDocumentationNoLock(Assembly assembly, TextReader textReader)
        {
            using XmlReader xmlReader = XmlReader.Create(textReader);
            while (xmlReader.Read())
            {
                if (xmlReader.NodeType is XmlNodeType.Element && xmlReader.Name is "member")
                {
                    string? rawName = xmlReader["name"];
                    if (!string.IsNullOrWhiteSpace(rawName))
                    {
                        LoadedXmlDocumentation[rawName] = xmlReader.ReadInnerXml();
                    }
                }
            }
        }

        public static void LoadXmlDocumentation(Assembly assembly, string xmlDocumentation)
        {
            using StringReader stringReader = new StringReader(xmlDocumentation);
            LoadXmlDocumentation(assembly, stringReader);
        }

        public static void LoadXmlDocumentation(Assembly assembly, TextReader textReader)
        {
            lock (XmlCacheLock)
            {
                LoadXmlDocumentationNoLock(assembly, textReader);
            }
        }

        internal static string? GetDocumentation(string key, Assembly assembly)
        {
            lock (XmlCacheLock)
            {
                if (LoadedXmlDocumentation.TryGetValue(key, out var value))
                {
                    return value;
                }

                if (LoadXmlDocumentation(assembly))
                {
                    if (LoadedXmlDocumentation.TryGetValue(key, out var newValue))
                    {
                        return newValue;
                    }
                }
                return null;
            }
        }

        private static string GetDocumentation(PropertyInfo propertyInfo)
        {
            var descriptionAttribute =
                propertyInfo
                    .GetCustomAttributes(typeof(DescriptionAttribute), false)
                    .FirstOrDefault();

            if (descriptionAttribute is DescriptionAttribute attribute && attribute.Description != "")
            {
                return attribute.Description;
            }

            if (propertyInfo.DeclaringType == null)
            {
                return "";
            }
            var documentation = GetDocumentation(GetXmlName(propertyInfo), propertyInfo.DeclaringType.Assembly);
            if (documentation != null)
            {
                var lines = documentation.Trim().Split("\n");
                var cleanedLines = lines.Select(line => line.Replace("<summary>", "")
                    .Replace("</summary>", "")
                    .Replace("///", "")
                    .Trim());

                return string.Join("\n", cleanedLines).TrimStart('\n').TrimEnd('\n');
            }

            return "";
        }

        private static string GetDocumentation(Type type)
        {
            var descriptionAttribute =
                type
                    .GetCustomAttributes(typeof(DescriptionAttribute), false)
                    .FirstOrDefault();

            if (descriptionAttribute is DescriptionAttribute attribute && attribute.Description != "")
            {
                return attribute.Description;
            }

            var documentation = GetDocumentation(GetXmlName(type), type.Assembly);
            if (documentation != null)
            {
                var lines = documentation.Trim().Split("\n");
                var cleanedLines = lines.Select(line => line.Replace("<summary>", "")
                    .Replace("</summary>", "")
                    .Replace("///", "")
                    .Trim());

                return string.Join("\n", cleanedLines).TrimStart('\n').TrimEnd('\n');
            }

            return "";
        }

        internal static string GetXmlNameTypeSegment(string typeFullNameString) =>
            Regex.Replace(typeFullNameString, @"\[.*\]", string.Empty).Replace('+', '.');

        public static string GetXmlName(Type type)
        {
            LoadXmlDocumentation(type.Assembly);
            return "T:" + GetXmlNameTypeSegment(type.FullName!);
        }

        public static string GetXmlName(PropertyInfo propertyInfo)
        {
            if (propertyInfo.DeclaringType == null)
                return "";

            return "P:" + GetXmlNameTypeSegment(propertyInfo.DeclaringType.FullName!) + "." + propertyInfo.Name;
        }

        private bool ContainsNullable(Type propertyType)
        {
            if (propertyType.IsGenericType)
            {
                if (propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    return true;
                }

                if (propertyType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var elementType = propertyType.GetGenericArguments()[0];
                    return ContainsNullable(elementType);
                }

                if (propertyType.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
                {
                    var elementType = propertyType.GetGenericArguments()[0];
                    return ContainsNullable(elementType);
                }

                if (propertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    var valueType = propertyType.GetGenericArguments()[1];
                    return ContainsNullable(valueType);
                }

                if (propertyType.GetGenericTypeDefinition() == typeof(Input<>))
                {
                    var elementType = propertyType.GetGenericArguments()[0];
                    return ContainsNullable(elementType);
                }

                if (propertyType.GetGenericTypeDefinition() == typeof(InputList<>))
                {
                    var elementType = propertyType.GetGenericArguments()[0];
                    return ContainsNullable(elementType);
                }

                if (propertyType.GetGenericTypeDefinition() == typeof(InputMap<>))
                {
                    var elementType = propertyType.GetGenericArguments()[0];
                    return ContainsNullable(elementType);
                }
            }

            if (propertyType.IsArray)
            {
                var elementType = propertyType.GetElementType()!;
                return ContainsNullable(elementType);
            }

            var assemblyFullName = propertyType.Assembly.FullName ?? "";
            if (propertyType.IsClass && !assemblyFullName.StartsWith("System."))
            {
                // treat classes as nullable by default
                // if users want these to be required, then they can use [Required]
                return true;
            }

            return false;
        }

        private JObject PropertyType(ComponentMetadata metadata, Type propertyType)
        {
            JObject Primitive(string typeName) => new JObject { ["type"] = typeName };

            if (propertyType == typeof(string))
            {
                return Primitive("string");
            }

            if (propertyType == typeof(int))
            {
                return Primitive("integer");
            }

            if (propertyType == typeof(bool))
            {
                return Primitive("bool");
            }

            if (propertyType == typeof(double))
            {
                return Primitive("number");
            }

            if (propertyType.IsArray)
            {
                var elementType = propertyType.GetElementType();
                return new JObject
                {
                    ["type"] = "array",
                    ["items"] = PropertyType(metadata, elementType!)
                };
            }

            if (propertyType.IsGenericType)
            {
                if (propertyType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var elementType = propertyType.GetGenericArguments()[0];
                    return new JObject
                    {
                        ["type"] = "array",
                        ["items"] = PropertyType(metadata, elementType!)
                    };
                }

                if (propertyType.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
                {
                    var elementType = propertyType.GetGenericArguments()[0];
                    return new JObject
                    {
                        ["type"] = "array",
                        ["items"] = PropertyType(metadata, elementType!)
                    };
                }

                if (propertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    // assume keys are strings
                    var valueType = propertyType.GetGenericArguments()[1];
                    return new JObject
                    {
                        ["type"] = "object",
                        ["additionalProperties"] = PropertyType(metadata, valueType)
                    };
                }

                if (propertyType.GetGenericTypeDefinition() == typeof(ImmutableDictionary<,>))
                {
                    // assume keys are strings
                    var valueType = propertyType.GetGenericArguments()[1];
                    return new JObject
                    {
                        ["type"] = "object",
                        ["additionalProperties"] = PropertyType(metadata, valueType)
                    };
                }

                if (propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    var elementType = propertyType.GetGenericArguments()[0];
                    return PropertyType(metadata, elementType);
                }
            }

            if (metadata.TypeReferences.ContainsKey(propertyType))
            {
                return new JObject
                {
                    ["$ref"] = metadata.TypeReferences[propertyType].FullReference
                };
            }

            return Primitive("object");
        }

        string DescriptionContent(PropertyInfo info)
        {
            var documentation = GetDocumentation(info);
            if (documentation != "")
            {
                return documentation;
            }



            return "";
        }

        string DescriptionAttributeContent(Type info)
        {
            var descriptionAttribute = info.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault();
            if (descriptionAttribute is DescriptionAttribute attribute)
            {
                return attribute.Description;
            }

            return "";
        }

        bool IsRequiredProperty(PropertyInfo property)
        {
            var inputAttr = property.GetCustomAttributes(typeof(InputAttribute), false).FirstOrDefault();
            if (inputAttr is InputAttribute attr)
            {
                if (attr.IsRequired)
                {
                    return true;
                }
            }

            var pulumiRequiredAttribute = property
                .GetCustomAttributes(typeof(Pulumi.RequiredAttribute), false)
                .FirstOrDefault();

            var requiredAttributeFromDataAnnotations = property
                .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute))
                .FirstOrDefault();

            return pulumiRequiredAttribute != null || requiredAttributeFromDataAnnotations != null;
        }

        bool IsOptionalProperty(PropertyInfo property)
        {
            var optionalAttribute = property
                .GetCustomAttributes(typeof(OptionalAttribute), false)
                .FirstOrDefault();

            return optionalAttribute != null;
        }

        bool IsSecretProperty(PropertyInfo property)
        {
            var secretAttribute = property
                .GetCustomAttributes(typeof(SecretAttribute), false)
                .FirstOrDefault();

            return secretAttribute != null;
        }

        public JObject BuildSchema()
        {
            var schema = new JObject();
            schema["name"] = ProviderName;
            schema["version"] = ProviderVersion;
            var resources = new JObject();
            var types = new JObject();
            foreach (var pair in ResourceMetadata)
            {
                var token = pair.Key;
                var metadata = pair.Value;
                var resource = new JObject();
                var inputProperties = new JObject();
                var properties = new JObject();
                var requiredInputs = new HashSet<string>();
                var requiredOutputs = new HashSet<string>();

                if (metadata.ArgsType != null)
                {
                    foreach (var property in metadata.ArgsType.GetProperties())
                    {
                        var propertyName = property.Name;
                        var propertyType = property.PropertyType;
                        var plain = true;
                        var secret = IsSecretProperty(property);
                        var inputAttr = property.GetCustomAttributes(typeof(InputAttribute), false).FirstOrDefault();
                        if (inputAttr is InputAttribute attr)
                        {
                            propertyName = attr.Name;
                        }

                        var requiredAttribute = property.GetCustomAttributes(typeof(RequiredAttribute), false)
                            .FirstOrDefault();


                        if (IsRequiredProperty(property) || !ContainsNullable(propertyType))
                        {
                            if (!IsOptionalProperty(property))
                            {
                                requiredInputs.Add(propertyName);
                            }
                        }

                        if (propertyType.IsGenericType)
                        {
                            if (propertyType.GetGenericTypeDefinition() == typeof(Input<>))
                            {
                                plain = false;
                                propertyType = propertyType.GetGenericArguments()[0];
                            }
                            else if (propertyType.GetGenericTypeDefinition() == typeof(InputList<>))
                            {
                                plain = false;
                                var elementType = propertyType.GetGenericArguments()[0];
                                propertyType = Array.CreateInstance(elementType, 0).GetType();
                            }
                            else if (propertyType.GetGenericTypeDefinition() == typeof(InputMap<>))
                            {
                                plain = false;
                                var elementType = propertyType.GetGenericArguments()[0];
                                propertyType = typeof(Dictionary<,>).MakeGenericType(typeof(string), elementType);
                            }
                            else if (propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                            {
                                propertyType = propertyType.GetGenericArguments()[0];
                            }
                        }

                        var propertySchema = PropertyType(metadata, propertyType);
                        if (plain)
                        {
                            propertySchema["plain"] = true;
                        }

                        if (secret && !plain)
                        {
                            // A property can only be a secret when it is an Output
                            propertySchema["secret"] = true;
                        }

                        var documentation = DescriptionContent(property);
                        if (documentation != "")
                        {
                            propertySchema["description"] = documentation;
                        }

                        if (property.Name != propertyName)
                        {
                            // if the name was overridden by an attribute, then add the original name as an alias
                            propertySchema["language"] = new JObject
                            {
                                ["csharp"] = new JObject
                                {
                                    ["name"] = property.Name
                                }
                            };
                        }

                        inputProperties[propertyName] = propertySchema;
                    }
                }

                // generate schema for each output property in the component
                foreach (var property in metadata.ResourceType.GetProperties())
                {
                    string propertyName = property.Name;
                    var outputAttr = property.GetCustomAttributes(typeof(OutputAttribute), false).FirstOrDefault();
                    if (outputAttr is OutputAttribute attr && attr.Name != "")
                    {
                        propertyName = attr.Name ?? "";
                    }

                    if (propertyName == "urn")
                    {
                        continue;
                    }

                    var propertyType = property.PropertyType;
                    if (propertyType.IsGenericType)
                    {
                        if (propertyType.GetGenericTypeDefinition() == typeof(Output<>))
                        {
                            propertyType = propertyType.GetGenericArguments()[0];
                        }
                    }

                    if (IsRequiredProperty(property) || !ContainsNullable(propertyType))
                    {
                        requiredOutputs.Add(propertyName);
                    }

                    var propertySchema = PropertyType(metadata, propertyType);
                    var documentation = GetDocumentation(property);
                    if (documentation != "")
                    {
                        propertySchema["description"] = documentation;
                    }

                    if (property.Name != propertyName)
                    {
                        // if the name was overridden by an attribute, then add the original name as an alias
                        propertySchema["language"] = new JObject
                        {
                            ["csharp"] = new JObject
                            {
                                ["name"] = property.Name
                            }
                        };
                    }

                    properties[propertyName] = propertySchema;
                }

                resource["isComponent"] = true;
                resource["inputProperties"] = inputProperties;
                resource["properties"] = properties;
                resource["requiredInputs"] = JToken.FromObject(requiredInputs.OrderBy(name => name));
                resource["required"] = JToken.FromObject(requiredOutputs.OrderBy(name => name));

                var resourceDescription = GetDocumentation(metadata.ResourceType);
                if (resourceDescription != "")
                {
                    resource["description"] = resourceDescription;
                }
                resources[token] = resource;

                foreach (var typeReferenceInfo in metadata.TypeReferences)
                {
                    var typeReference = typeReferenceInfo.Value;
                    if (typeReference.IsExternal)
                    {
                        continue;
                    }

                    if (!types.ContainsKey(typeReference.Token))
                    {
                        var typeSchema = new JObject();
                        var typeDescription = GetDocumentation(typeReferenceInfo.Key);
                        if (typeDescription != "")
                        {
                            typeSchema["description"] = typeDescription;
                        }
                        typeDescription = DescriptionAttributeContent(typeReferenceInfo.Key);
                        if (typeDescription != "")
                        {
                            typeSchema["description"] = typeDescription;
                        }

                        if (typeReferenceInfo.Key.IsEnum)
                        {
                            var enumsCases = new JArray();
                            foreach (var name in Enum.GetNames(typeReferenceInfo.Key))
                            {
                                enumsCases.Add(name);
                            }

                            typeSchema["type"] = "string";
                            typeSchema["enum"] = enumsCases;
                        }
                        else
                        {
                            var typeProperties = new JObject();
                            var requiredTypeProperties = new HashSet<string>();
                            foreach (var property in typeReferenceInfo.Key.GetProperties())
                            {
                                var propertyName = property.Name;
                                var outputAttr = property.GetCustomAttributes(typeof(OutputAttribute), false)
                                    .FirstOrDefault();
                                if (outputAttr is OutputAttribute output && output.Name != "")
                                {
                                    propertyName = output.Name ?? "";
                                }

                                var inputAttr = property.GetCustomAttributes(typeof(InputAttribute), false)
                                    .FirstOrDefault();
                                if (inputAttr is InputAttribute input && input.Name != "")
                                {
                                    propertyName = input.Name ?? "";
                                }

                                var propertyType = property.PropertyType;
                                if (propertyType.IsGenericType)
                                {
                                    if (propertyType.GetGenericTypeDefinition() == typeof(Output<>))
                                    {
                                        propertyType = propertyType.GetGenericArguments()[0];
                                    }
                                    if (propertyType.GetGenericTypeDefinition() == typeof(Input<>))
                                    {
                                        propertyType = propertyType.GetGenericArguments()[0];
                                    }
                                    else if (propertyType.GetGenericTypeDefinition() == typeof(InputList<>))
                                    {
                                        var elementType = propertyType.GetGenericArguments()[0];
                                        propertyType = Array.CreateInstance(elementType, 0).GetType();
                                    }
                                    else if (propertyType.GetGenericTypeDefinition() == typeof(InputMap<>))
                                    {
                                        var elementType = propertyType.GetGenericArguments()[0];
                                        propertyType = typeof(Dictionary<,>).MakeGenericType(typeof(string), elementType);
                                    }
                                    else if (propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                                    {
                                        propertyType = propertyType.GetGenericArguments()[0];
                                    }
                                }

                                if (IsRequiredProperty(property) || !ContainsNullable(property.PropertyType))
                                {
                                    if (!IsOptionalProperty(property))
                                    {
                                        requiredTypeProperties.Add(propertyName);
                                    }
                                }

                                var propertySchema = PropertyType(metadata, propertyType);
                                var documentation = GetDocumentation(property);
                                if (documentation != "")
                                {
                                    propertySchema["description"] = documentation;
                                }

                                typeProperties[propertyName] = propertySchema;
                            }

                            typeSchema["type"] = "object";
                            typeSchema["properties"] = typeProperties;
                            typeSchema["required"] = JToken.FromObject(requiredTypeProperties.OrderBy(name => name));
                        }

                        types[typeReference.Token] = typeSchema;
                    }
                }
            }

            schema["types"] = types;
            schema["resources"] = resources;
            return _extendSchema(schema);
        }

        public ComponentProvider Build() => new ComponentProvider(this);
    }

    public class ComponentProvider : Provider
    {
        private ComponentProviderBuilder _builder;

        public ComponentProvider(ComponentProviderBuilder builder)
        {
            _builder = builder;
        }

        public static ComponentProviderBuilder Create() => new ComponentProviderBuilder();

        public override Task<GetSchemaResponse> GetSchema(GetSchemaRequest request, CancellationToken ct)
        {
            var response = new GetSchemaResponse();
            response.Schema = _builder.BuildSchema().ToString();
            return Task.FromResult(response);
        }

        public override async Task<ConstructResponse> Construct(ConstructRequest request, CancellationToken ct)
        {
            if (_builder.ResourceConstructors.TryGetValue(request.Type, out var constructor))
            {
                var argsType = _builder.ResourceMetadata[request.Type].ArgsType;
                var args =
                    argsType != null
                        ? PropertyValue.DeserializeObject(request.Inputs, argsType)
                        : null;

                if (argsType != null && args == null)
                {
                    throw new Exception($"Failed to deserialize args of type {argsType.FullName} when constructing component of type {request.Type}");
                }

                var resource = constructor(request, args ?? new object());
                var urn = await resource.ResolveUrn();
                var state = await PropertyValue.StateFromComponentResource(resource);
                return new ConstructResponse(urn, state);
            }

            throw new Exception($"Unexpected token {request.Type}");
        }
        public override Task<ConfigureResponse> Configure(ConfigureRequest request, CancellationToken ct)
        {
            var response = new ConfigureResponse();
            response.AcceptOutputs = false;
            response.AcceptSecrets = true;
            response.AcceptResources = false;
            response.SupportsPreview = true;
            return Task.FromResult(response);
        }

        public async Task Serve(string[] args)
        {
            await Provider.Serve(args, _builder.ProviderVersion, host => this, CancellationToken.None);
        }

        public async Task Serve(string[] args, CancellationToken ct)
        {
            await Provider.Serve(args, _builder.ProviderVersion, host => this, ct);
        }
    }
}

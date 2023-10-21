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
    public class ComponentMetadata
    {
        public readonly Type ArgsType;
        public readonly Type ResourceType;

        public ComponentMetadata(Type argsType, Type resourceType)
        {
            ArgsType = argsType;
            ResourceType = resourceType;
        }
    }

    public class ComponentProviderBuilder
    {
        public readonly Dictionary<string, Func<ConstructRequest, object, ComponentResource>> ResourceConstructors = new Dictionary<string, Func<ConstructRequest, object, ComponentResource>>();
        public readonly Dictionary<string, ComponentMetadata> ResourceMetadata = new Dictionary<string, ComponentMetadata>();
        public string ProviderName { get; set; } = "provider";
        public string ProviderVersion { get; set; } = "0.1.0";

        public ComponentProviderBuilder Register<T, U>(string type, Func<ConstructRequest, T, U> constructor)
            where T : ResourceArgs
            where U : ComponentResource
        {
            ResourceConstructors[type] = (request, args) => constructor(request, (T)args);
            ResourceMetadata[type] = new ComponentMetadata(argsType: typeof(T), resourceType: typeof(U));
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

        private JObject PropertyType(Type propertyType)
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
                    ["items"] = PropertyType(elementType!)
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
                        ["items"] = PropertyType(elementType!)
                    };
                }

                if (propertyType.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
                {
                    var elementType = propertyType.GetGenericArguments()[0];
                    return new JObject
                    {
                        ["type"] = "array",
                        ["items"] = PropertyType(elementType!)
                    };
                }

                if (propertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    // assume keys are strings
                    var valueType = propertyType.GetGenericArguments()[1];
                    return new JObject
                    {
                        ["type"] = "object",
                        ["addtionalProperties"] = PropertyType(valueType)
                    };
                }

                if (propertyType.GetGenericTypeDefinition() == typeof(ImmutableDictionary<,>))
                {
                    // assume keys are strings
                    var valueType = propertyType.GetGenericArguments()[1];
                    return new JObject
                    {
                        ["type"] = "object",
                        ["addtionalProperties"] = PropertyType(valueType)
                    };
                }
            }

            return Primitive("object");
        }

        string DescriptionAttributeContent(PropertyInfo info)
        {
            var descriptionAttribute = info.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault();
            if (descriptionAttribute is DescriptionAttribute attribute)
            {
                return attribute.Description;
            }

            return "";
        }

        public JObject BuildSchema()
        {
            var schema = new JObject();
            schema["name"] = ProviderName;
            schema["version"] = ProviderVersion;
            var resources = new JObject();
            foreach (var pair in ResourceMetadata)
            {
                var token = pair.Key;
                var metadata = pair.Value;
                var resource = new JObject();
                var inputProperties = new JObject();
                var properties = new JObject();
                var requiredInputs = new HashSet<string>();
                var requiredOutputs = new HashSet<string>();
                foreach(var property in metadata.ArgsType.GetProperties())
                {
                    var propertyName = property.Name;
                    var propertyType = property.PropertyType;
                    var plain = true;
                    var secret = false;
                    var inputAttr = property.GetCustomAttributes(typeof(InputAttribute), false).FirstOrDefault();
                    if (inputAttr is InputAttribute attr)
                    {
                        propertyName = attr.Name;
                        if (attr.IsRequired)
                        {
                            requiredInputs.Add(propertyName);
                        }
                    }

                    var requiredAttribute = property.
                        GetCustomAttributes(typeof(RequiredAttribute), false)
                        .FirstOrDefault();

                    if (requiredAttribute != null)
                    {
                        // if using Pulumi.RequiredAttribute, then mark the property as required
                        requiredInputs.Add(propertyName);
                    }

                    var requiredAttributeFromDataAnnotations = property
                        .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute))
                        .FirstOrDefault();

                    if (requiredAttributeFromDataAnnotations != null)
                    {
                        // if using System.ComponentModel.DataAnnotations.RequiredAttribute,
                        // then mark the property as required
                        requiredInputs.Add(propertyName);
                    }

                    var secretAttribute = property.GetCustomAttributes(typeof(SecretAttribute), false).FirstOrDefault();
                    if (secretAttribute != null)
                    {
                        secret = true;
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
                    }

                    var propertySchema = PropertyType(propertyType);
                    if (plain)
                    {
                        propertySchema["plain"] = true;
                    }

                    if (secret && !plain)
                    {
                        // A property can only be a secret when it is an Output
                        propertySchema["secret"] = true;
                    }

                    var documentation = GetDocumentation(property);
                    if (documentation != "")
                    {
                        propertySchema["description"] = documentation;
                    }

                    var descriptionAttributeContent = DescriptionAttributeContent(property);
                    if (descriptionAttributeContent != "")
                    {
                        propertySchema["description"] = descriptionAttributeContent;
                    }

                    propertySchema["language"] = new JObject
                    {
                        ["csharp"] = new JObject
                        {
                            ["name"] = property.Name
                        }
                    };

                    inputProperties[propertyName] = propertySchema;
                }

                foreach (var property in metadata.ResourceType.GetProperties())
                {
                    string propertyName = property.Name;
                    var outputAttr = property.GetCustomAttributes(typeof(OutputAttribute), false).FirstOrDefault();
                    if (outputAttr is OutputAttribute attr && attr.Name != "")
                    {
                        propertyName = attr.Name ?? "";
                    }

                    var requiredAttribute = property.GetCustomAttributes(typeof(RequiredAttribute), false).FirstOrDefault();
                    if (requiredAttribute != null)
                    {
                        requiredOutputs.Add(propertyName);
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

                    var propertySchema = PropertyType(propertyType);
                    var documentation = GetDocumentation(property);
                    if (documentation != "")
                    {
                        propertySchema["description"] = documentation;
                    }

                    var descriptionAttributeContent = DescriptionAttributeContent(property);
                    if (descriptionAttributeContent != "")
                    {
                        propertySchema["description"] = descriptionAttributeContent;
                    }

                    propertySchema["language"] = new JObject
                    {
                        ["csharp"] = new JObject
                        {
                            ["name"] = property.Name
                        }
                    };

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
            }

            schema["resources"] = resources;
            return schema;
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
                var args = PropertyValue.DeserializeObject(request.Inputs, argsType);
                if (args == null)
                {
                    throw new Exception($"Failed to deserialize args of type {argsType}");
                }

                var resource = constructor(request, args);
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

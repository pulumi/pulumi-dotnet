using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pulumi.Utilities;
namespace Pulumi.Experimental.Provider
{
    public class ComponentProviderImplementation : Provider
    {
        private readonly Assembly componentAssembly;
        private readonly string packageName;
#pragma warning disable CS0618 // Type or member is obsolete
        private readonly PropertyValueSerializer serializer;
#pragma warning restore CS0618 // Type or member is obsolete

        public ComponentProviderImplementation(Assembly? componentAssembly, string? packageName)
        {
            this.componentAssembly = componentAssembly ?? Assembly.GetCallingAssembly();
            this.packageName = packageName ?? this.componentAssembly.GetName().Name!.ToLower();
#pragma warning disable CS0618 // Type or member is obsolete
            this.serializer = new PropertyValueSerializer();
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public override Task<GetSchemaResponse> GetSchema(GetSchemaRequest request, CancellationToken ct)
        {
            var analyzer = new Analyzer(new Metadata 
            { 
                Name = packageName,
                Version = "1.0.0" // This should probably come from the provider configuration
            });

            var (components, typeDefinitions) = analyzer.Analyze(componentAssembly);
            var schema = PackageSpec.GenerateSchema(
                metadata: new Metadata 
                { 
                    Name = packageName,
                    Version = "1.0.0",
                    // You might want to add DisplayName from provider config
                },
                components: components,
                typeDefinitions: typeDefinitions
            );

            // Serialize to JSON
            var jsonSchema = JsonSerializer.Serialize(schema, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull 
            });

            // throw new Exception(jsonSchema);

            return Task.FromResult(new GetSchemaResponse
            {
                Schema = jsonSchema
            });
        }

        public override async Task<ConstructResponse> Construct(ConstructRequest request, CancellationToken ct)
        {
            try {
            // Parse type token
            var parts = request.Type.Split(':');
            if (parts.Length != 3 || parts[0] != packageName)
                throw new ArgumentException($"Invalid resource type: {request.Type}");

            var componentName = parts[2];
            var componentType = componentAssembly.GetType(componentName) 
                ?? throw new ArgumentException($"Component type not found: {componentName}");

            // Create args instance by deserializing inputs
            var argsType = GetArgsType(componentType);
            var args = serializer.Deserialize(new PropertyValue(request.Inputs), argsType);

            // Create component instance
            var component = (ComponentResource)Activator.CreateInstance(componentType, request.Name, args, request.Options)!;

            var urn = await OutputUtilities.GetValueAsync(component.Urn);
            if (string.IsNullOrEmpty(urn))
            {
                throw new InvalidOperationException($"URN of resource {request.Name} is not known.");
            }

            var stateValue = await serializer.StateFromComponentResource(component);

            return new ConstructResponse(new Experimental.Provider.Urn(urn), stateValue, ImmutableDictionary<string, ISet<Urn>>.Empty);
            }
            catch (Exception e)
            {
                throw new Exception($"Error constructing resource {request.Name}: {e.Message} {e.StackTrace}");
            }
        }

        private static Type GetArgsType(Type componentType)
        {
            var constructor = componentType.GetConstructors().First();
            var argsParameter = constructor.GetParameters()
                .FirstOrDefault(p => p.Name == "args") 
                ?? throw new ArgumentException($"Component {componentType.Name} must have an 'args' parameter in constructor");
            
            return argsParameter.ParameterType;
        }
    }
}

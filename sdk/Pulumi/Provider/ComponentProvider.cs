using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Humanizer;
using Pulumi.Utilities;

namespace Pulumi.Experimental.Provider
{
    /// <summary>
    /// A provider that can be used to construct components from a given assembly with automatic schema inference.
    /// </summary>
    public class ComponentProvider : Provider
    {
        private readonly Assembly componentAssembly;
        private readonly Metadata metadata;
        private readonly Type[]? componentTypes;
#pragma warning disable CS0618 // Type or member is obsolete
        private readonly PropertyValueSerializer serializer;
#pragma warning restore CS0618 // Type or member is obsolete

        /// <summary>
        /// Creates a new component provider.
        /// </summary>
        /// <param name="componentAssembly">The assembly containing component types</param>
        /// <param name="metadata">The metadata for the package</param>
        /// <param name="componentTypes">Optional array of known component types</param>
        public ComponentProvider(Assembly componentAssembly, Metadata metadata, Type[]? componentTypes = null)
        {
            this.componentAssembly = componentAssembly;
            this.metadata = metadata;
            this.componentTypes = componentTypes;
#pragma warning disable CS0618 // Type or member is obsolete
            this.serializer = new PropertyValueSerializer();
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Gets the schema for the components in the assembly.
        /// </summary>
        /// <param name="request">The request containing the package name</param>
        /// <param name="ct">The cancellation token</param>
        /// <returns>The schema for the components in the assembly</returns>
        public override Task<GetSchemaResponse> GetSchema(GetSchemaRequest request, CancellationToken ct)
        {
            var schema = componentTypes != null
                ? ComponentAnalyzer.GenerateSchema(metadata, componentTypes)
                : ComponentAnalyzer.GenerateSchema(metadata, componentAssembly);

            // Serialize to JSON
            var jsonSchema = JsonSerializer.Serialize(schema, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            return Task.FromResult(new GetSchemaResponse
            {
                Schema = jsonSchema
            });
        }

        /// <summary>
        /// Constructs a component resource.
        /// </summary>
        /// <param name="request">The request containing the component type and inputs</param>
        /// <param name="ct">The cancellation token</param>
        /// <returns>The constructed component resource</returns>
        /// <exception cref="ArgumentException">If the resource type is invalid</exception>
        /// <exception cref="InvalidOperationException">If the component type is not found or cannot be constructed</exception>
        public override async Task<ConstructResponse> Construct(ConstructRequest request, CancellationToken ct)
        {
            // Parse type token
            var parts = request.Type.Split(':');
            if (parts.Length != 3 || parts[0] != metadata.Name)
                throw new ArgumentException($"Invalid resource type: {request.Type}");

            var componentName = parts[2];
            var componentType = ComponentAnalyzer.FindComponentType(componentName, componentAssembly, componentTypes)
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

            return new ConstructResponse(new Urn(urn), stateValue, ImmutableDictionary<string, ISet<Urn>>.Empty);
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

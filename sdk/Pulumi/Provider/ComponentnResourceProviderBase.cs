using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Pulumi.Utilities;

namespace Pulumi.Experimental.Provider;

public class ComponentResourceProviderBase : Experimental.Provider.Provider
{
    protected async Task<ConstructResponse> Construct<TResource, TArgs>(
        ConstructRequest request,
        Func<string, TArgs, ComponentResourceOptions, Task<TResource>> factory
    )
        where TResource : ComponentResource
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var serializer = new PropertyValueSerializer();
#pragma warning restore CS0618 // Type or member is obsolete
        var args = await serializer.Deserialize<TArgs>(new PropertyValue(request.Inputs));
        var resource = await factory(request.Name, args, request.Options);

        var urn = await OutputUtilities.GetValueAsync(resource.Urn);
        if (string.IsNullOrEmpty(urn))
        {
            throw new InvalidOperationException($"URN of resource {request.Name} is not known.");
        }

        var stateValue = await serializer.Serialize(resource);
        if (!stateValue.TryGetObject(out var state))
        {
            throw new InvalidOperationException($"Resource {urn} did not serialize to an object");
        }

        state = state.Remove(nameof(resource.Urn).ToLowerInvariant());

        return new ConstructResponse(urn, state, ImmutableDictionary<string, PropertyDependencies>.Empty);
    }
}
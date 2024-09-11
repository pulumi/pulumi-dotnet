using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Pulumi.Experimental.Provider;
using Pulumi.Utilities;

namespace Pulumi.IntegrationTests.Utils;

public class ComponentResourceProviderBase : Provider
{
    protected async Task<CallResponse> Call<TArgs, TReturn>(
        CallRequest request,
        Func<ResourceReference?, TArgs, Output<CheckResult>> check,
        Func<ResourceReference?, TArgs, Output<TReturn>> factory
    ) where TReturn : class
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var serializer = new PropertyValueSerializer();
#pragma warning restore CS0618 // Type or member is obsolete
        var args = await serializer.Deserialize<TArgs>(new PropertyValue(request.Args));

        var checkResult = await OutputUtilities.GetValueAsync(check(request.Self, args));
        if (!checkResult.IsValid)
        {
            return new CallResponse(null, checkResult.Failures, ImmutableDictionary<string, ISet<Experimental.Provider.Urn>>.Empty);
        }

        var result = await OutputUtilities.GetValueAsync(factory(request.Self, args));

        var serializedResult = await serializer.Serialize(result);

        if (!serializedResult.TryGetObject(out var resultObject))
        {
            throw new InvalidOperationException("Expected result to be an object");
        }

        return new CallResponse(resultObject, new List<CheckFailure>(), ImmutableDictionary<string, ISet<Experimental.Provider.Urn>>.Empty);
    }

    protected async Task<ConstructResponse> Construct<TArgs, TResource>(
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

        var stateValue = await serializer.StateFromComponentResource(resource);

        return new ConstructResponse(new Experimental.Provider.Urn(urn), stateValue, ImmutableDictionary<string, ISet<Experimental.Provider.Urn>>.Empty);
    }

    public override Task<ConfigureResponse> Configure(ConfigureRequest request, CancellationToken ct)
    {
        return Task.FromResult(new ConfigureResponse()
        {
            AcceptOutputs = true,
            AcceptResources = true,
            AcceptSecrets = true,
            SupportsPreview = true
        });
    }
}

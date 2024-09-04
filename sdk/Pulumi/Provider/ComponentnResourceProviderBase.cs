using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Pulumi.Utilities;

namespace Pulumi.Experimental.Provider;

public class CheckResult
{
    public static readonly CheckResult Empty = new CheckResult();
    public bool IsValid => Failures.Count == 0;
    public IList<CheckFailure> Failures { get; set; }

    private CheckResult()
        : this(ImmutableList<CheckFailure>.Empty)
    {
    }

    public CheckResult(IList<CheckFailure> failures)
    {
        Failures = failures;
    }
}

public class ComponentResourceProviderBase : Provider
{
    protected Task<CallResponse> Call<TArgs, TReturn>(
        CallRequest request,
        Func<ResourceReference?, TArgs, Output<TReturn>> factory
    ) where TReturn : class
    {
        return Call(request, (_, _) => CheckResult.Empty, factory);
    }

    protected Task<CallResponse> Call<TArgs, TReturn>(
        CallRequest request,
        Func<ResourceReference?, TArgs, CheckResult> check,
        Func<ResourceReference?, TArgs, Output<TReturn>> factory
    ) where TReturn : class
    {
        return Call(request, (self, args) => Task.FromResult(check(self, args)), factory);
    }

    protected async Task<CallResponse> Call<TArgs, TReturn>(
        CallRequest request,
        Func<ResourceReference?, TArgs, Task<CheckResult>> check,
        Func<ResourceReference?, TArgs, Output<TReturn>> factory
    ) where TReturn : class
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var serializer = new PropertyValueSerializer();
#pragma warning restore CS0618 // Type or member is obsolete
        var args = await serializer.Deserialize<TArgs>(new PropertyValue(request.Args));

        var checkResult = await check(request.Self, args);
        if (!checkResult.IsValid)
        {
            return new CallResponse(null, checkResult.Failures, ImmutableDictionary<string, ISet<Urn>>.Empty);
        }

        var result = await OutputUtilities.GetValueAsync(factory(request.Self, args));

        var serializedResult = await serializer.Serialize(result);

        if (!serializedResult.TryGetObject(out var resultObject))
        {
            throw new InvalidOperationException("Expected result to be an object");
        }


        return new CallResponse(resultObject, new List<CheckFailure>(), ImmutableDictionary<string, ISet<Urn>>.Empty);
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

        return new ConstructResponse(new Urn(urn), stateValue, ImmutableDictionary<string, ISet<Urn>>.Empty);
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

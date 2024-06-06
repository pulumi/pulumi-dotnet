using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Pulumirpc;

namespace Pulumi.Experimental.Provider;

class ResourceProviderService : ResourceProvider.ResourceProviderBase, IDisposable
{
    readonly Func<IHost, Provider> factory;
    readonly CancellationTokenSource rootCTS;
    Provider? implementation;
    readonly string version;

    /** Queue of construct calls. */
    private volatile Task constructCallQueue = Task.CompletedTask;

    Provider Implementation
    {
        get
        {
            if (implementation == null)
            {
                throw new RpcException(new Status(StatusCode.FailedPrecondition, "Engine host not yet attached"));
            }

            return implementation;
        }
    }

    private void CreateProvider(string address)
    {
        var host = new GrpcHost(address);
        implementation = factory(host);
    }

    public ResourceProviderService(Func<IHost, Provider> factory, IConfiguration configuration)
    {
        this.factory = factory;
        this.rootCTS = new CancellationTokenSource();

        var host = configuration.GetValue<string?>("Host", null);
        if (host != null)
        {
            CreateProvider(host);
        }

        var version = configuration.GetValue<string?>("Version", null);
        if (version == null)
        {
            var entryAssembly = System.Reflection.Assembly.GetEntryAssembly();
            Debug.Assert(entryAssembly != null, "GetEntryAssembly returned null in managed code");
            var entryName = entryAssembly.GetName();
            var assemblyVersion = entryName.Version;
            if (assemblyVersion != null)
            {
                // Pulumi expects semver style versions, so we convert from the .NET version format by
                // dropping the revision component.
                version = string.Format("{0}.{1}.{2}", assemblyVersion.Major, assemblyVersion.Minor, assemblyVersion.Build);
            }
            else
            {
                throw new Exception("Provider.Serve must be called with a version, or an assembly version must be set.");
            }
        }

        this.version = version;
    }

    public void Dispose()
    {
        this.rootCTS.Dispose();
    }

    public override Task<Empty> Attach(Pulumirpc.PluginAttach request, ServerCallContext context)
    {
        CreateProvider(request.Address);
        return Task.FromResult(new Empty());
    }

    public override Task<Empty> Cancel(Empty request, ServerCallContext context)
    {
        try
        {
            this.rootCTS.Cancel();
            return Task.FromResult(new Empty());
        }
        catch (NotImplementedException ex)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    private CancellationTokenSource GetToken(ServerCallContext context)
    {
        return CancellationTokenSource.CreateLinkedTokenSource(rootCTS.Token, context.CancellationToken);
    }

    // Helper to deal with the fact that at the GRPC layer any Struct property might be null. For those we just want to return empty dictionaries at this level.
    // This keeps the PropertyValue.Marshal clean in terms of not handling nulls.
    private ImmutableDictionary<string, PropertyValue> Marshal(Struct? properties)
    {
        if (properties == null)
        {
            return ImmutableDictionary<string, PropertyValue>.Empty;
        }

        return PropertyValue.Unmarshal(properties);
    }

    public override async Task<Pulumirpc.CheckResponse> CheckConfig(Pulumirpc.CheckRequest request, ServerCallContext context)
    {
        try
        {
            var domRequest = new CheckRequest(request.Urn, Marshal(request.Olds), Marshal(request.News),
                ImmutableArray.ToImmutableArray(request.RandomSeed));
            using var cts = GetToken(context);
            var domResponse = await Implementation.CheckConfig(domRequest, cts.Token);
            var grpcResponse = new Pulumirpc.CheckResponse();
            grpcResponse.Inputs = domResponse.Inputs == null ? null : PropertyValue.Marshal(domResponse.Inputs);
            if (domResponse.Failures != null)
            {
                grpcResponse.Failures.AddRange(MappFailures(domResponse.Failures));
            }

            return grpcResponse;
        }
        catch (NotImplementedException ex)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<Pulumirpc.DiffResponse> DiffConfig(Pulumirpc.DiffRequest request, ServerCallContext context)
    {
        try
        {
            var domRequest = new DiffRequest(request.Urn, request.Id, Marshal(request.Olds), Marshal(request.News),
                request.IgnoreChanges.ToImmutableArray());
            using var cts = GetToken(context);
            var domResponse = await Implementation.DiffConfig(domRequest, cts.Token);
            var grpcResponse = new Pulumirpc.DiffResponse();
            if (domResponse.Changes.HasValue)
            {
                grpcResponse.Changes = domResponse.Changes.Value
                    ? Pulumirpc.DiffResponse.Types.DiffChanges.DiffSome
                    : Pulumirpc.DiffResponse.Types.DiffChanges.DiffNone;
            }

            if (domResponse.Stables != null)
            {
                grpcResponse.Stables.AddRange(domResponse.Stables);
            }

            if (domResponse.Replaces != null)
            {
                grpcResponse.Replaces.AddRange(domResponse.Replaces);
            }

            grpcResponse.DeleteBeforeReplace = domResponse.DeleteBeforeReplace;
            if (domResponse.Diffs != null)
            {
                grpcResponse.Diffs.AddRange(domResponse.Diffs);
            }

            if (domResponse.DetailedDiff != null)
            {
                foreach (var item in domResponse.DetailedDiff)
                {
                    var domDiff = item.Value;
                    var grpcDiff = new Pulumirpc.PropertyDiff();
                    grpcDiff.InputDiff = domDiff.InputDiff;
                    grpcDiff.Kind = (Pulumirpc.PropertyDiff.Types.Kind)domDiff.Kind;
                    grpcResponse.DetailedDiff.Add(item.Key, grpcDiff);
                }
            }

            return grpcResponse;
        }
        catch (NotImplementedException ex)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<Pulumirpc.InvokeResponse> Invoke(Pulumirpc.InvokeRequest request, ServerCallContext context)
    {
        try
        {
            var domRequest = new InvokeRequest(request.Tok, Marshal(request.Args));
            using var cts = GetToken(context);
            var domResponse = await Implementation.Invoke(domRequest, cts.Token);
            var grpcResponse = new Pulumirpc.InvokeResponse();
            grpcResponse.Return = domResponse.Return == null ? null : PropertyValue.Marshal(domResponse.Return);
            if (domResponse.Failures != null)
            {
                grpcResponse.Failures.AddRange(MappFailures(domResponse.Failures));
            }

            return grpcResponse;
        }
        catch (NotImplementedException ex)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<Pulumirpc.GetSchemaResponse> GetSchema(Pulumirpc.GetSchemaRequest request, ServerCallContext context)
    {
        try
        {
            var domRequest = new GetSchemaRequest(request.Version);
            using var cts = GetToken(context);
            var domResponse = await Implementation.GetSchema(domRequest, cts.Token);
            var grpcResponse = new Pulumirpc.GetSchemaResponse();
            grpcResponse.Schema = domResponse.Schema ?? "";
            return grpcResponse;
        }
        catch (NotImplementedException ex)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<Pulumirpc.ConfigureResponse> Configure(Pulumirpc.ConfigureRequest request, ServerCallContext context)
    {
        try
        {
            var domRequest = new ConfigureRequest(request.Variables.ToImmutableDictionary(), Marshal(request.Args), request.AcceptSecrets,
                request.AcceptResources);
            using var cts = GetToken(context);
            var domResponse = await Implementation.Configure(domRequest, cts.Token);
            var grpcResponse = new Pulumirpc.ConfigureResponse();
            grpcResponse.AcceptSecrets = domResponse.AcceptSecrets;
            grpcResponse.SupportsPreview = domResponse.SupportsPreview;
            grpcResponse.AcceptResources = domResponse.AcceptResources;
            grpcResponse.AcceptOutputs = domResponse.AcceptOutputs;
            return grpcResponse;
        }
        catch (NotImplementedException ex)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override Task<Pulumirpc.PluginInfo> GetPluginInfo(Empty request, ServerCallContext context)
    {
        try
        {
            using var cts = GetToken(context);
            var grpcResponse = new Pulumirpc.PluginInfo();
            grpcResponse.Version = this.version;
            return Task.FromResult(grpcResponse);
        }
        catch (NotImplementedException ex)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<Pulumirpc.CreateResponse> Create(Pulumirpc.CreateRequest request, ServerCallContext context)
    {
        try
        {
            var domRequest = new CreateRequest(request.Urn, Marshal(request.Properties), TimeSpan.FromSeconds(request.Timeout), request.Preview);
            using var cts = GetToken(context);
            var domResponse = await Implementation.Create(domRequest, cts.Token);
            var grpcResponse = new Pulumirpc.CreateResponse();
            grpcResponse.Id = domResponse.Id ?? "";
            grpcResponse.Properties = domResponse.Properties == null ? null : PropertyValue.Marshal(domResponse.Properties);
            return grpcResponse;
        }
        catch (NotImplementedException ex)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<Pulumirpc.ReadResponse> Read(Pulumirpc.ReadRequest request, ServerCallContext context)
    {
        try
        {
            var domRequest = new ReadRequest(request.Urn, request.Id, Marshal(request.Properties), Marshal(request.Inputs));
            using var cts = GetToken(context);
            var domResponse = await Implementation.Read(domRequest, cts.Token);
            var grpcResponse = new Pulumirpc.ReadResponse();
            grpcResponse.Id = domResponse.Id ?? "";
            grpcResponse.Properties = domResponse.Properties == null ? null : PropertyValue.Marshal(domResponse.Properties);
            grpcResponse.Inputs = domResponse.Inputs == null ? null : PropertyValue.Marshal(domResponse.Inputs);
            return grpcResponse;
        }
        catch (NotImplementedException ex)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<Pulumirpc.CheckResponse> Check(Pulumirpc.CheckRequest request, ServerCallContext context)
    {
        try
        {
            var domRequest = new CheckRequest(request.Urn, Marshal(request.Olds), Marshal(request.News),
                ImmutableArray.ToImmutableArray(request.RandomSeed));
            using var cts = GetToken(context);
            var domResponse = await Implementation.Check(domRequest, cts.Token);
            var grpcResponse = new Pulumirpc.CheckResponse();
            grpcResponse.Inputs = domResponse.Inputs == null ? null : PropertyValue.Marshal(domResponse.Inputs);
            if (domResponse.Failures != null)
            {
                grpcResponse.Failures.AddRange(MappFailures(domResponse.Failures));
            }

            return grpcResponse;
        }
        catch (NotImplementedException ex)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<Pulumirpc.DiffResponse> Diff(Pulumirpc.DiffRequest request, ServerCallContext context)
    {
        try
        {
            var domRequest = new DiffRequest(request.Urn, request.Id, Marshal(request.Olds), Marshal(request.News),
                request.IgnoreChanges.ToImmutableArray());
            using var cts = GetToken(context);
            var domResponse = await Implementation.Diff(domRequest, cts.Token);
            var grpcResponse = new Pulumirpc.DiffResponse();
            if (domResponse.Changes.HasValue)
            {
                grpcResponse.Changes = domResponse.Changes.Value
                    ? Pulumirpc.DiffResponse.Types.DiffChanges.DiffSome
                    : Pulumirpc.DiffResponse.Types.DiffChanges.DiffNone;
            }

            if (domResponse.Stables != null)
            {
                grpcResponse.Stables.AddRange(domResponse.Stables);
            }

            if (domResponse.Replaces != null)
            {
                grpcResponse.Replaces.AddRange(domResponse.Replaces);
            }

            grpcResponse.DeleteBeforeReplace = domResponse.DeleteBeforeReplace;
            if (domResponse.Diffs != null)
            {
                grpcResponse.Diffs.AddRange(domResponse.Diffs);
            }

            if (domResponse.DetailedDiff != null)
            {
                foreach (var item in domResponse.DetailedDiff)
                {
                    var domDiff = item.Value;
                    var grpcDiff = new Pulumirpc.PropertyDiff();
                    grpcDiff.InputDiff = domDiff.InputDiff;
                    grpcDiff.Kind = (Pulumirpc.PropertyDiff.Types.Kind)domDiff.Kind;
                    grpcResponse.DetailedDiff.Add(item.Key, grpcDiff);
                }
            }

            return grpcResponse;
        }
        catch (NotImplementedException ex)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<Pulumirpc.UpdateResponse> Update(Pulumirpc.UpdateRequest request, ServerCallContext context)
    {
        try
        {
            var domRequest = new UpdateRequest(request.Urn, request.Id, Marshal(request.Olds), Marshal(request.News), TimeSpan.FromSeconds(request.Timeout),
                request.IgnoreChanges.ToImmutableArray(), request.Preview);
            using var cts = GetToken(context);
            var domResponse = await Implementation.Update(domRequest, cts.Token);
            var grpcResponse = new Pulumirpc.UpdateResponse();
            grpcResponse.Properties = domResponse.Properties == null ? null : PropertyValue.Marshal(domResponse.Properties);
            return grpcResponse;
        }
        catch (NotImplementedException ex)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<Empty> Delete(Pulumirpc.DeleteRequest request, ServerCallContext context)
    {
        try
        {
            var domRequest = new DeleteRequest(request.Urn, request.Id, Marshal(request.Properties), TimeSpan.FromSeconds(request.Timeout));
            using var cts = GetToken(context);
            await Implementation.Delete(domRequest, cts.Token);
            return new Empty();
        }
        catch (NotImplementedException ex)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<Pulumirpc.ConstructResponse> Construct(Pulumirpc.ConstructRequest request, ServerCallContext context)
    {
        try
        {
            var aliases = request.Aliases.Select(urn => (Input<Alias>)new Alias() { Urn = urn }).ToList();

            InputList<Resource> dependsOn = request.Dependencies
                .Select(urn => new DependencyResource(urn))
                .ToImmutableArray<Resource>();
            var providers = request.Providers.Values
                .Select(reference => new DependencyProviderResource(reference))
                .ToList<ProviderResource>();

            var opts = new ComponentResourceOptions()
            {
                Aliases = aliases,
                DependsOn = dependsOn,
                Protect = request.Protect,
                Providers = providers,
                Parent = request.Parent != null ? new DependencyResource(request.Parent) : null,
            };

            var domRequest = new ConstructRequest(request.Name, request.Type, Marshal(request.Inputs), opts);
            using var cts = GetToken(context);
            var domResponse = await Implementation.Construct(domRequest, cts.Token);
            var state = PropertyValue.Marshal(domResponse.State, out var stateDependencies);

            var grpcResponse = new Pulumirpc.ConstructResponse
            {
                Urn = domResponse.Urn,
                State = state,
            };
            grpcResponse.StateDependencies.Add(domResponse.StateDependencies.ToDictionary(kv => kv.Key, kv => BuildPropertyDependencies(kv.Value)));

            foreach (var stateDependency in stateDependencies)
            {
                if (grpcResponse.StateDependencies.TryGetValue(stateDependency.Key, out var existing))
                {
                    existing.Urns.AddRange(stateDependency.Value.Urns);
                }
                else
                {
                    grpcResponse.StateDependencies.Add(stateDependency.Key, BuildPropertyDependencies(stateDependency.Value));
                }
            }

            return grpcResponse;
        }
        catch (NotImplementedException ex)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<Pulumirpc.CallResponse> Call(Pulumirpc.CallRequest request, ServerCallContext context)
    {
        try
        {
            var domArgs = Marshal(request.Args);

            domArgs = PatchArgDependencies(request, domArgs);

            var domRequest = new CallRequest(request.Tok, domArgs);
            using var cts = GetToken(context);
            var domResponse = await Implementation.Call(domRequest, cts.Token);

            IDictionary<string, PropertyDependencies> returnDependencies = ImmutableDictionary<string, PropertyDependencies>.Empty;
            var grpcResponse = new Pulumirpc.CallResponse
            {
                Return = domResponse.Return == null ? null : PropertyValue.Marshal(domResponse.Return, out returnDependencies)
            };
            grpcResponse.ReturnDependencies.Add(domResponse.ReturnDependencies.ToDictionary(kv => kv.Key, kv => BuildReturnDependencies(kv.Value)));

            foreach (var returnDependency in returnDependencies)
            {
                if (grpcResponse.ReturnDependencies.TryGetValue(returnDependency.Key, out var existing))
                {
                    existing.Urns.AddRange(returnDependency.Value.Urns);
                }
                else
                {
                    grpcResponse.ReturnDependencies.Add(returnDependency.Key, BuildReturnDependencies(returnDependency.Value));
                }
            }

            if (domResponse.Failures != null)
            {
                grpcResponse.Failures.AddRange(MappFailures(domResponse.Failures));
            }

            return grpcResponse;
        }
        catch (NotImplementedException ex)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    private static ImmutableDictionary<string, PropertyValue> PatchArgDependencies(Pulumirpc.CallRequest request,
        ImmutableDictionary<string, PropertyValue> domArgs)
    {
        foreach (var argDependency in request.ArgDependencies)
        {
            if (domArgs.TryGetValue(argDependency.Key, out var currentValue))
            {
                domArgs = domArgs.SetItem(argDependency.Key,
                    new PropertyValue(new OutputReference(currentValue, argDependency.Value.Urns.ToImmutableArray())));
            }
        }

        return domArgs;
    }

    private static Pulumirpc.ConstructResponse.Types.PropertyDependencies BuildPropertyDependencies(PropertyDependencies dependencies)
    {
        var propertyDependencies = new Pulumirpc.ConstructResponse.Types.PropertyDependencies();
        propertyDependencies.Urns.AddRange(dependencies.Urns);
        return propertyDependencies;
    }

    private static Pulumirpc.CallResponse.Types.ReturnDependencies BuildReturnDependencies(PropertyDependencies dependencies)
    {
        var propertyDependencies = new Pulumirpc.CallResponse.Types.ReturnDependencies();
        propertyDependencies.Urns.AddRange(dependencies.Urns);
        return propertyDependencies;
    }

    private static IEnumerable<Pulumirpc.CheckFailure> MappFailures(IEnumerable<CheckFailure> failures)
    {
        return failures.Select(domFailure => new Pulumirpc.CheckFailure
        {
            Property = domFailure.Property,
            Reason = domFailure.Reason
        });
    }
}
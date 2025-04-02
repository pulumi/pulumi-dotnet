using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Pulumi.Experimental.Provider.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pulumi.Experimental.Provider
{
    public sealed class CheckRequest<I, C>
    {
        public readonly Urn Urn;
        public string Type => Pulumi.Urn.Type(Urn);
        public string Name => Pulumi.Urn.Name(Urn);
        public readonly C? OldInputs;
        public readonly I NewInputs;
        public readonly ImmutableArray<byte> RandomSeed;

        public CheckRequest(Urn urn,
           C? oldInputs,
           I newInputs,
            ImmutableArray<byte> randomSeed)
        {
            Urn = urn;
            OldInputs = oldInputs;
            NewInputs = newInputs;
            RandomSeed = randomSeed;
        }
    }

    public sealed class CheckResponse<C>
    {
        public C? Inputs { get; set; }
        public IList<CheckFailure>? Failures { get; set; }
    }

    public sealed class DiffRequest<C, O>
    {
        public readonly Urn Urn;
        public string Type => Pulumi.Urn.Type(Urn);
        public string Name => Pulumi.Urn.Name(Urn);
        public readonly string Id;
        public readonly O? OldState;
        public readonly C NewInputs;
        public readonly ImmutableArray<string> IgnoreChanges;

        public DiffRequest(Urn urn,
            string id,
            O? oldState,
            C newInputs,
            ImmutableArray<string> ignoreChanges)
        {
            Urn = urn;
            Id = id;
            OldState = oldState;
            NewInputs = newInputs;
            IgnoreChanges = ignoreChanges;
        }
    }


    public sealed class InvokeRequest<I>
    {
        public readonly string Tok;
        public readonly ImmutableDictionary<string, PropertyValue> Args;

        public InvokeRequest(string tok, ImmutableDictionary<string, PropertyValue> args)
        {
            Tok = tok;
            Args = args;
        }
    }

    public sealed class InvokeResponse<O>
    {
        public IDictionary<string, PropertyValue>? Return { get; set; }
        public IList<CheckFailure>? Failures { get; set; }
    }

    public sealed class ConfigureRequest<I>
    {
        public readonly ImmutableDictionary<string, string> Variables;
        public readonly ImmutableDictionary<string, PropertyValue> Args;
        public readonly bool AcceptSecrets;
        public readonly bool AcceptResources;

        public ConfigureRequest(ImmutableDictionary<string, string> variables,
            ImmutableDictionary<string, PropertyValue> args,
            bool acceptSecrets,
            bool acceptResources)
        {
            Variables = variables;
            Args = args;
            AcceptSecrets = acceptSecrets;
            AcceptResources = acceptResources;
        }
    }


    public sealed class CreateRequest<C>
    {
        public readonly Urn Urn;
        public string Type => Pulumi.Urn.Type(Urn);
        public string Name => Pulumi.Urn.Name(Urn);
        public readonly C Properties;
        public readonly TimeSpan Timeout;
        public readonly bool Preview;

        public CreateRequest(Urn urn, C properties, TimeSpan timeout, bool preview)
        {
            Urn = urn;
            Properties = properties;
            Timeout = timeout;
            Preview = preview;
        }
    }

    public sealed class CreateResponse<O>
    {
        public string? Id { get; set; }
        public O? Properties { get; set; }
    }

    public sealed class ReadRequest<C, O>
    {
        public readonly Urn Urn;
        public readonly string Id;
        public string Type => Pulumi.Urn.Type(Urn);
        public string Name => Pulumi.Urn.Name(Urn);
        public readonly O? Properties;
        public readonly C? Inputs;

        public ReadRequest(Urn urn, string id, O? properties, C? inputs)
        {
            Urn = urn;
            Id = id;
            Properties = properties;
            Inputs = inputs;
        }
    }

    public sealed class ReadResponse<C, O>
    {
        public string? Id { get; set; }
        public O? Properties { get; set; }
        public C? Inputs { get; set; }
    }

    public sealed class UpdateRequest<C, O>
    {
        public readonly Urn Urn;
        public readonly string Id;
        public string Type => Pulumi.Urn.Type(Urn);
        public string Name => Pulumi.Urn.Name(Urn);
        public readonly ImmutableDictionary<string, PropertyValue> Olds;
        public readonly ImmutableDictionary<string, PropertyValue> News;
        public readonly TimeSpan Timeout;
        public readonly ImmutableArray<string> IgnoreChanges;
        public readonly bool Preview;

        public UpdateRequest(Urn urn,
            string id,
            ImmutableDictionary<string, PropertyValue> olds,
            ImmutableDictionary<string, PropertyValue> news,
            TimeSpan timeout,
            ImmutableArray<string> ignoreChanges,
            bool preview)
        {
            Urn = urn;
            Id = id;
            Olds = olds;
            News = news;
            Timeout = timeout;
            IgnoreChanges = ignoreChanges;
            Preview = preview;
        }
    }

    public sealed class UpdateResponse<O>
    {
        public IDictionary<string, PropertyValue>? Properties { get; set; }
    }

    public sealed class DeleteRequest<O>
    {
        public readonly Urn Urn;
        public readonly string Id;
        public string Type => Pulumi.Urn.Type(Urn);
        public string Name => Pulumi.Urn.Name(Urn);
        public readonly O Properties;
        public readonly TimeSpan Timeout;

        public DeleteRequest(Urn urn, string id, O properties, TimeSpan timeout)
        {
            Urn = urn;
            Id = id;
            Properties = properties;
            Timeout = timeout;
        }
    }

    public sealed class ConstructRequest<I>
    {
        public string Type { get; init; }
        public string Name { get; init; }
        public ImmutableDictionary<string, PropertyValue> Inputs { get; init; }
        public ComponentResourceOptions Options { get; init; }

        public ConstructRequest(string type, string name, ImmutableDictionary<string, PropertyValue> inputs, ComponentResourceOptions options)
        {
            Type = type;
            Name = name;
            Inputs = inputs;
            Options = options;
        }
    }

    public sealed class ConstructResponse<O>
    {
        public Urn Urn { get; init; }
        public IDictionary<string, PropertyValue> State { get; init; }
        public IDictionary<string, ISet<Urn>> StateDependencies { get; init; }

        public ConstructResponse(Urn urn, IDictionary<string, PropertyValue> state, IDictionary<string, ISet<Urn>> stateDependencies)
        {
            Urn = urn;
            State = state;
            StateDependencies = stateDependencies;
        }
    }

    public sealed class CallRequest<I>
    {
        public ResourceReference? Self { get; }
        public string Tok { get; init; }
        public ImmutableDictionary<string, PropertyValue> Args { get; init; }

        public CallRequest(ResourceReference? self, string tok, ImmutableDictionary<string, PropertyValue> args)
        {
            Self = self;
            Tok = tok;
            Args = args;
        }
    }

    public sealed class CallResponse<O>
    {
        public IDictionary<string, PropertyValue>? Return { get; init; }
        public IDictionary<string, ISet<Urn>> ReturnDependencies { get; init; }
        public IList<CheckFailure>? Failures { get; init; }

        public CallResponse(IDictionary<string, PropertyValue>? @return,
            IList<CheckFailure>? failures,
            IDictionary<string, ISet<Urn>> returnDependencies)
        {
            Return = @return;
            Failures = failures;
            ReturnDependencies = returnDependencies;
        }
    }

    internal abstract class ProviderApply<T>
    {
        public abstract T Apply<I>(Provider<I> provider);
    }

    public abstract class ProviderCrate
    {
        internal ProviderCrate()
        {
        }

        internal abstract R Apply<R>(ProviderApply<R> apply);

        public static ProviderCrate New<I>(Provider<I> provider)
        {
            return new ProviderCrate<I>(provider);
        }
    }

    sealed class ProviderCrate<I> : ProviderCrate
    {
        private readonly Provider<I> _provider;

        public ProviderCrate(Provider<I> provider)
        {
            _provider = provider;
        }

        internal override R Apply<R>(ProviderApply<R> apply)
        {
            return apply.Apply(_provider);
        }
    }

    public abstract class Provider<I>
    {
        public virtual Task<CheckResponse<I>> CheckConfig(CheckRequest<I, I> request, CancellationToken ct)
        {
            return Task.FromResult(new CheckResponse<I>()
            {
                Inputs = request.NewInputs,
            });
        }

        public virtual Task<DiffResponse> DiffConfig(DiffRequest<I, I> request, CancellationToken ct)
        {
            return Task.FromResult(new DiffResponse());
        }

        public virtual Task<ConfigureResponse> Configure(ConfigureRequest<I> request, CancellationToken ct)
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

    internal abstract class CustomResourceApply<T>
    {
        public abstract T Apply<I, C, O>(CustomResource<I, C, O> provider) where I : class where C : class where O : class;
    }

    public abstract class CustomResourceCrate
    {
        internal CustomResourceCrate()
        {
        }

        internal abstract R Apply<R>(CustomResourceApply<R> apply);

        public static CustomResourceCrate New<I, C, O>(CustomResource<I, C, O> resource)
            where I : class where C : class where O : class
        {
            return new CustomResourceCrate<I, C, O>(resource);
        }
    }

    sealed class CustomResourceCrate<I, C, O> : CustomResourceCrate where I : class where C : class where O : class
    {
        private readonly CustomResource<I, C, O> _resource;

        public CustomResourceCrate(CustomResource<I, C, O> resource)
        {
            _resource = resource;
        }

        internal override R Apply<R>(CustomResourceApply<R> apply)
        {
            return apply.Apply(_resource);
        }
    }


    public abstract class CustomResource<I, C, O> where I : class where C : class where O : class
    {
        public virtual Task<CreateResponse<O>> Create(CreateRequest<C> request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(Create)}' is not implemented ");
        }

        public virtual Task<ReadResponse<C, O>> Read(ReadRequest<C, O> request, CancellationToken ct)
        {
            return Task.FromResult(new ReadResponse<C, O>
            {
                Id = request.Id,
                Properties = request.Properties,
                Inputs = request.Inputs,
            });
        }

        public virtual Task<CheckResponse<C>> Check(CheckRequest<I, C> request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(Check)}' is not implemented ");
        }

        public virtual Task<DiffResponse> Diff(DiffRequest<C, O> request, CancellationToken ct)
        {
            // The default implementation for diff is to return unknown changes, so to let the engine
            // handle the diff.
            return Task.FromResult(new DiffResponse());
        }

        public virtual Task<UpdateResponse<O>> Update(UpdateRequest<C, O> request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(Update)}' is not implemented ");
        }

        public virtual Task Delete(DeleteRequest<O> request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(Delete)}' is not implemented ");
        }
    }


    public abstract class Function<I, C, O>
    {
        public virtual Task<InvokeResponse> Invoke(InvokeRequest request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(Invoke)}' is not implemented ");
        }
    }


    public abstract class Method<I, C, O>
    {
        public virtual Task<CallResponse> Call(CallRequest request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(Call)}' is not implemented ");
        }
    }


    public abstract class Component<I, C, O>
    {
        public virtual Task<ConstructResponse> Construct(ConstructRequest request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(Construct)}' is not implemented ");
        }
    }

    public sealed class InferredProvider : Provider
    {

        string _name;
        string _version;

        Func<ParameterizeRequest, CancellationToken, Task<Provider>>? _parameterize;

        Provider? _provider;

        ProviderCrate? _providerCrate;
        ImmutableDictionary<string, CustomResourceCrate>? _resources;

        public InferredProvider(string name, string version, ProviderCrate provider, ImmutableDictionary<string, CustomResourceCrate> resources)
        {
            _name = name;
            _version = version;
            _providerCrate = provider;
            _resources = resources;
        }

        public InferredProvider(string name, string version,
            Func<ParameterizeRequest, CancellationToken, Task<Provider>> parameterize)
        {
            _name = name;
            _version = version;
            _parameterize = parameterize;
        }

        public InferredProvider(string name, string version,
            ProviderCrate provider, ImmutableDictionary<string, CustomResourceCrate> resources,
            Func<ParameterizeRequest, CancellationToken, Task<Provider>> parameterize)
        {
            _name = name;
            _version = version;
            _providerCrate = provider;
            _resources = resources;
            _parameterize = parameterize;
        }

        public override async Task<ParameterizeResponse> Parameterize(ParameterizeRequest request, CancellationToken ct)
        {
            if (_parameterize is null)
            {
                throw new NotImplementedException($"The method '{nameof(Parameterize)}' is not implemented ");
            }

            _provider = await _parameterize(request, ct);
            _parameterize = null;

            return new ParameterizeResponse(_name, _version);
        }

        sealed class GetSchemaApply : ProviderApply<PropertySchema>
        {
            public override PropertySchema Apply<I>(Provider<I> provider)
            {
                var options = PropertyValueSerializerOptions.Default;
                var converter = options.GetConverter(typeof(I));
                return converter.GetSchema(typeof(I));
            }
        }

        sealed class GetResourceSchemaApply : CustomResourceApply<(PropertySchema, PropertySchema)>
        {
            public override (PropertySchema, PropertySchema) Apply<I, C, O>(CustomResource<I, C, O> provider)
            {
                var options = PropertyValueSerializerOptions.Default;

                var converter = options.GetConverter(typeof(I));
                var input = converter.GetSchema(typeof(I));

                converter = options.GetConverter(typeof(O));
                var output = converter.GetSchema(typeof(O));
                return (input, output);
            }
        }

        public override Task<GetSchemaResponse> GetSchema(GetSchemaRequest request, CancellationToken ct)
        {
            if (_provider is not null)
            {
                return _provider.GetSchema(request, ct);
            }

            if (_resources is null)
            {
                throw new NotImplementedException($"The method '{nameof(GetSchema)}' is not implemented ");
            }

            //var providerSchema = _provider.Apply(new GetSchemaApply());

            var resources = new Dictionary<string, ResourceSpec>();
            var resourceApply = new GetResourceSchemaApply();
            foreach (var kv in _resources)
            {
                var (i, o) = kv.Value.Apply(resourceApply);
                resources.Add(_name + ":" + kv.Key, PropertySchema.ToResourceSpec(i, o));
            }

            var packageSpec = new PackageSpec
            {
                Name = _name,
                Version = _version,
                Meta = ImmutableSortedDictionary<string, object>.Empty.Add("supportPack", true),
                Resources = ImmutableSortedDictionary.CreateRange(resources),
            };

            var jsonSchema = JsonSerializer.Serialize(packageSpec, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            return Task.FromResult(new GetSchemaResponse
            {
                Schema = jsonSchema
            });
        }

        private static T Deserialize<T>(ImmutableDictionary<string, PropertyValue> value)
        {
            var properties =
                PropertyValueSerialiser.Deserialize<T>(
                    new PropertyValue(value));

            if (properties is null)
            {
                throw new InvalidOperationException($"Failed to deserialize properties of type {typeof(T).FullName}");
            }

            return properties;
        }

        private static async Task<ImmutableDictionary<string, PropertyValue>?> Serialize<T>(T? value)
        {
            if (value is null)
            {
                return null;
            }

            var properties = await PropertyValueSerialiser.Serialize(value);
            if (properties is null)
            {
                throw new InvalidOperationException($"Failed to serialize properties of type {typeof(T).FullName}");
            }
            if (!properties.TryGetObject(out var obj))
            {
                throw new InvalidOperationException($"Failed to serialize properties of type {typeof(T).FullName}");
            }

            return ImmutableDictionary.CreateRange(obj);
        }

        sealed class CreateApply : CustomResourceApply<Task<CreateResponse>>
        {
            private readonly CreateRequest _request;
            private readonly CancellationToken _ct;

            public CreateApply(CreateRequest request, CancellationToken ct)
            {
                _request = request;
                _ct = ct;
            }

            public override async Task<CreateResponse> Apply<I, C, O>(CustomResource<I, C, O> resource)
                where I : class where C : class where O : class
            {
                var request = new CreateRequest<C>(
                    _request.Urn,
                    Deserialize<C>(_request.Properties),
                    _request.Timeout,
                    _request.Preview
                );

                var response = await resource.Create(request, _ct);

                return new CreateResponse
                {
                    Id = response.Id,
                    Properties = await Serialize(response.Properties),
                };
            }
        }

        private CustomResourceCrate getResource(string type, [System.Runtime.CompilerServices.CallerMemberName] string methodName = "")
        {
            if (_resources is null)
            {
                throw new NotImplementedException($"The method '{methodName}' is not implemented ");
            }

            // Split off the package name
            if (!type.StartsWith(_name + ":", StringComparison.Ordinal))
            {
                throw new NotImplementedException($"The resource '{type}' is not implemented ");
            }
            type = type.Substring(_name.Length + 1);

            if (!_resources.TryGetValue(type, out var resource))
            {
                throw new NotImplementedException($"The resource '{type}' is not implemented ");
            }
            return resource;
        }

        public override Task<CreateResponse> Create(CreateRequest request, CancellationToken ct)
        {
            if (_provider is not null)
            {
                return _provider.Create(request, ct);
            }

            var resource = getResource(request.Type);
            return resource.Apply(new CreateApply(request, ct));
        }


        sealed class CheckApply : CustomResourceApply<Task<CheckResponse>>
        {
            private readonly CheckRequest _request;
            private readonly CancellationToken _ct;

            public CheckApply(CheckRequest request, CancellationToken ct)
            {
                _request = request;
                _ct = ct;
            }

            public override async Task<CheckResponse> Apply<I, C, O>(CustomResource<I, C, O> resource)
                where I : class where C : class where O : class
            {
                // If OldInputs is empty assume it's null. It means we can't actually tell the
                // difference between null and empty but, actually empty is unlikely, and fixing this 
                // I think needs wire engine changes.
                C? olds = null;
                if (_request.OldInputs.Count > 0)
                {
                    olds = Deserialize<C>(_request.OldInputs);
                }

                var request = new CheckRequest<I, C>(
                    _request.Urn,
                    olds,
                    Deserialize<I>(_request.NewInputs),
                    _request.RandomSeed
                );

                var response = await resource.Check(request, _ct);

                return new CheckResponse
                {
                    Inputs = await Serialize<C>(response.Inputs),
                    Failures = response.Failures,
                };
            }
        }


        public override Task<CheckResponse> Check(CheckRequest request, CancellationToken ct)
        {
            if (_provider is not null)
            {
                return _provider.Check(request, ct);
            }

            var resource = getResource(request.Type);
            return resource.Apply(new CheckApply(request, ct));
        }


        sealed class DiffApply : CustomResourceApply<Task<DiffResponse>>
        {
            private readonly DiffRequest _request;
            private readonly CancellationToken _ct;

            public DiffApply(DiffRequest request, CancellationToken ct)
            {
                _request = request;
                _ct = ct;
            }

            public override async Task<DiffResponse> Apply<I, C, O>(CustomResource<I, C, O> resource)
                where I : class where C : class where O : class
            {
                O? olds = null;
                if (_request.OldState.Count > 0)
                {
                    olds = Deserialize<O>(_request.OldState);
                }

                var request = new DiffRequest<C, O>(
                    _request.Urn,
                    _request.Id,
                    olds,
                    Deserialize<C>(_request.NewInputs),
                    _request.IgnoreChanges
                );

                return await resource.Diff(request, _ct);
            }
        }

        public override Task<DiffResponse> Diff(DiffRequest request, CancellationToken ct)
        {
            if (_provider is not null)
            {
                return _provider.Diff(request, ct);
            }

            var resource = getResource(request.Type);
            return resource.Apply(new DiffApply(request, ct));
        }


        sealed class DeleteApply : CustomResourceApply<Task>
        {
            private readonly DeleteRequest _request;
            private readonly CancellationToken _ct;

            public DeleteApply(DeleteRequest request, CancellationToken ct)
            {
                _request = request;
                _ct = ct;
            }

            public override async Task Apply<I, C, O>(CustomResource<I, C, O> resource)
                where I : class where C : class where O : class
            {
                var request = new DeleteRequest<O>(
                    _request.Urn,
                    _request.Id,
                    Deserialize<O>(_request.Properties),
                    _request.Timeout
                );

                await resource.Delete(request, _ct);
            }
        }
        public override Task Delete(DeleteRequest request, CancellationToken ct)
        {
            if (_provider is not null)
            {
                return _provider.Delete(request, ct);
            }

            var resource = getResource(request.Type);
            return resource.Apply(new DeleteApply(request, ct));
        }



        sealed class ReadApply : CustomResourceApply<Task<ReadResponse>>
        {
            private readonly ReadRequest _request;
            private readonly CancellationToken _ct;

            public ReadApply(ReadRequest request, CancellationToken ct)
            {
                _request = request;
                _ct = ct;
            }

            public override async Task<ReadResponse> Apply<I, C, O>(CustomResource<I, C, O> resource)
                where I : class where C : class where O : class
            {
                O? props = null;
                if (_request.Properties.Count > 0)
                {
                    props = Deserialize<O>(_request.Properties);
                }

                C? inputs = null;
                if (_request.Inputs.Count > 0)
                {
                    inputs = Deserialize<C>(_request.Inputs);
                }

                var request = new ReadRequest<C, O>(
                    _request.Urn,
                    _request.Id,
                    props,
                    inputs
                );

                var response = await resource.Read(request, _ct);

                return new ReadResponse
                {
                    Id = response.Id,
                    Properties = await Serialize<O>(response.Properties),
                    Inputs = await Serialize<C>(response.Inputs),
                };
            }
        }

        public override Task<ReadResponse> Read(ReadRequest request, CancellationToken ct)
        {
            if (_provider is not null)
            {
                return _provider.Read(request, ct);
            }

            var resource = getResource(request.Type);
            return resource.Apply(new ReadApply(request, ct));
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Pulumi.Experimental.Provider
{
    public sealed class CheckRequest
    {
        public readonly string Urn;
        // Note the Go SDK directly exposes resource.URN and so providers can work with it directly. I've
        // decided _not_ to copy that to the dotnet SDK on the basis that long term I'd like URNs to be opaque
        // tokens to everything but the engine. If CheckRequests need the resource type and name they should
        // be sent as separate string fields by the engine, rather than expecting every language to correctly
        // parse URNs. But for now we're half-waying this by having the public dotnet API expose Type and Name
        // directly, but by parsing the single URN sent from the engine.
        public string Type => Pulumi.Urn.Type(Urn);
        public string Name => Pulumi.Urn.Name(Urn);
        public readonly ImmutableDictionary<string, PropertyValue> OldInputs;
        public readonly ImmutableDictionary<string, PropertyValue> NewInputs;
        public readonly ImmutableArray<byte> RandomSeed;

        public CheckRequest(string urn, ImmutableDictionary<string, PropertyValue> oldInputs, ImmutableDictionary<string, PropertyValue> newInputs, ImmutableArray<byte> randomSeed)
        {
            Urn = urn;
            OldInputs = oldInputs;
            NewInputs = newInputs;
            RandomSeed = randomSeed;
        }
    }

    public sealed class CheckFailure
    {
        public string Property { get; set; }
        public string Reason { get; set; }

        public CheckFailure(string property, string reason)
        {
            Property = property;
            Reason = reason;
        }
    }

    public sealed class CheckResponse
    {
        public IDictionary<string, PropertyValue>? Inputs { get; set; }
        public IList<CheckFailure>? Failures { get; set; }
    }


    public sealed class DiffRequest
    {
        public readonly string Urn;
        public string Type => Pulumi.Urn.Type(Urn);
        public string Name => Pulumi.Urn.Name(Urn);
        public readonly string Id;
        public readonly ImmutableDictionary<string, PropertyValue> OldState;
        public readonly ImmutableDictionary<string, PropertyValue> NewInputs;
        public readonly ImmutableArray<string> IgnoreChanges;

        public DiffRequest(string urn, string id, ImmutableDictionary<string, PropertyValue> oldState, ImmutableDictionary<string, PropertyValue> newInputs, ImmutableArray<string> ignoreChanges)
        {
            Urn = urn;
            Id = id;
            OldState = oldState;
            NewInputs = newInputs;
            IgnoreChanges = ignoreChanges;
        }
    }

    public enum PropertyDiffKind
    {
        Add = 0,
        AddReplace = 1,
        Delete = 2,
        DeleteReplace = 3,
        Update = 4,
        UpdateReplace = 5,
    }

    public sealed class PropertyDiff
    {
        public PropertyDiffKind Kind { get; set; }
        public bool InputDiff { get; set; }
    }

    public sealed class DiffResponse
    {
        public bool? Changes { get; set; }

        public IList<string>? Replaces { get; set; }

        public IList<string>? Stables { get; set; }

        public bool DeleteBeforeReplace { get; set; }
        public IList<string>? Diffs { get; set; }

        public IDictionary<string, PropertyDiff>? DetailedDiff { get; set; }
    }

    public sealed class InvokeRequest
    {
        public readonly string Tok;
        public readonly ImmutableDictionary<string, PropertyValue> Args;

        public InvokeRequest(string tok, ImmutableDictionary<string, PropertyValue> args)
        {
            Tok = tok;
            Args = args;
        }
    }

    public sealed class InvokeResponse
    {
        public IDictionary<string, PropertyValue>? Return { get; set; }
        public IList<CheckFailure>? Failures { get; set; }
    }

    public sealed class GetSchemaRequest
    {
        public readonly int Version;

        public GetSchemaRequest(int version)
        {
            Version = version;
        }
    }

    public sealed class GetSchemaResponse
    {
        public string? Schema { get; set; }
    }

    public sealed class ConfigureRequest
    {
        public readonly ImmutableDictionary<string, string> Variables;
        public readonly ImmutableDictionary<string, PropertyValue> Args;
        public readonly bool AcceptSecrets;
        public readonly bool AcceptResources;

        public ConfigureRequest(ImmutableDictionary<string, string> variables, ImmutableDictionary<string, PropertyValue> args, bool acceptSecrets, bool acceptResources)
        {
            Variables = variables;
            Args = args;
            AcceptSecrets = acceptSecrets;
            AcceptResources = acceptResources;
        }
    }

    public sealed class ConfigureResponse
    {
        public bool AcceptSecrets { get; set; }
        public bool SupportsPreview { get; set; }
        public bool AcceptResources { get; set; }
        public bool AcceptOutputs { get; set; }
    }

    public sealed class CreateRequest
    {
        public readonly string Urn;
        public string Type => Pulumi.Urn.Type(Urn);
        public string Name => Pulumi.Urn.Name(Urn);
        public readonly ImmutableDictionary<string, PropertyValue> Properties;
        public readonly TimeSpan Timeout;
        public readonly bool Preview;

        public CreateRequest(string urn, ImmutableDictionary<string, PropertyValue> properties, TimeSpan timeout, bool preview)
        {
            Urn = urn;
            Properties = properties;
            Timeout = timeout;
            Preview = preview;
        }
    }

    public sealed class CreateResponse
    {
        public string? Id { get; set; }
        public IDictionary<string, PropertyValue>? Properties { get; set; }
    }

    public sealed class ReadRequest
    {
        public readonly string Urn;
        public readonly string Id;
        public string Type => Pulumi.Urn.Type(Urn);
        public string Name => Pulumi.Urn.Name(Urn);
        public readonly ImmutableDictionary<string, PropertyValue> Properties;
        public readonly ImmutableDictionary<string, PropertyValue> Inputs;

        public ReadRequest(string urn, string id, ImmutableDictionary<string, PropertyValue> properties, ImmutableDictionary<string, PropertyValue> inputs)
        {
            Urn = urn;
            Id = id;
            Properties = properties;
            Inputs = inputs;
        }
    }

    public sealed class ReadResponse
    {
        public string? Id { get; set; }
        public IDictionary<string, PropertyValue>? Properties { get; set; }
        public IDictionary<string, PropertyValue>? Inputs { get; set; }
    }

    public sealed class UpdateRequest
    {
        public readonly string Urn;
        public readonly string Id;
        public string Type => Pulumi.Urn.Type(Urn);
        public string Name => Pulumi.Urn.Name(Urn);
        public readonly ImmutableDictionary<string, PropertyValue> Olds;
        public readonly ImmutableDictionary<string, PropertyValue> News;
        public readonly TimeSpan Timeout;
        public readonly ImmutableArray<string> IgnoreChanges;
        public readonly bool Preview;

        public UpdateRequest(string urn,
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

    public sealed class UpdateResponse
    {
        public IDictionary<string, PropertyValue>? Properties { get; set; }
    }

    public sealed class DeleteRequest
    {
        public readonly string Urn;
        public readonly string Id;
        public string Type => Pulumi.Urn.Type(Urn);
        public string Name => Pulumi.Urn.Name(Urn);
        public readonly ImmutableDictionary<string, PropertyValue> Properties;
        public readonly TimeSpan Timeout;

        public DeleteRequest(string urn, string id, ImmutableDictionary<string, PropertyValue> properties, TimeSpan timeout)
        {
            Urn = urn;
            Id = id;
            Properties = properties;
            Timeout = timeout;
        }
    }

    public sealed class ConstructRequest
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

    public sealed class ConstructResponse
    {
        public string Urn { get; init; }
        public IDictionary<string, PropertyValue> State { get; init; }
        public IDictionary<string, PropertyDependencies> StateDependencies { get; init; }

        public ConstructResponse(string urn, IDictionary<string, PropertyValue> state, IDictionary<string, PropertyDependencies> stateDependencies)
        {
            Urn = urn;
            State = state;
            StateDependencies = stateDependencies;
        }
    }

    public sealed class CallRequest
    {
        public string Tok { get; init; }
        public ImmutableDictionary<string, PropertyValue> Args { get; init; }

        public CallRequest(string tok, ImmutableDictionary<string, PropertyValue> args)
        {
            Tok = tok;
            Args = args;
        }
    }

    public sealed class CallResponse
    {
        public IDictionary<string, PropertyValue>? Return { get; init; }
        public IDictionary<string, PropertyDependencies> ReturnDependencies { get; init; }
        public IList<CheckFailure>? Failures { get; init; }

        public CallResponse(IDictionary<string, PropertyValue>? @return, IList<CheckFailure>? failures, IDictionary<string, PropertyDependencies> returnDependencies)
        {
            Return = @return;
            Failures = failures;
            ReturnDependencies = returnDependencies;
        }
    }

    public abstract class Provider
    {
        public virtual Task<GetSchemaResponse> GetSchema(GetSchemaRequest request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(GetSchema)}' is not implemented ");
        }

        public virtual Task<CheckResponse> CheckConfig(CheckRequest request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(CheckConfig)}' is not implemented ");
        }

        public virtual Task<DiffResponse> DiffConfig(DiffRequest request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(DiffConfig)}' is not implemented ");
        }

        public virtual Task<ConfigureResponse> Configure(ConfigureRequest request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(Configure)}' is not implemented ");
        }

        public virtual Task<InvokeResponse> Invoke(InvokeRequest request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(Invoke)}' is not implemented ");
        }

        public virtual Task<CreateResponse> Create(CreateRequest request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(Create)}' is not implemented ");
        }

        public virtual Task<ReadResponse> Read(ReadRequest request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(Read)}' is not implemented ");
        }

        public virtual Task<CheckResponse> Check(CheckRequest request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(Check)}' is not implemented ");
        }

        public virtual Task<DiffResponse> Diff(DiffRequest request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(Diff)}' is not implemented ");
        }

        public virtual Task<UpdateResponse> Update(UpdateRequest request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(Update)}' is not implemented ");
        }

        public virtual Task Delete(DeleteRequest request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(Delete)}' is not implemented ");
        }

        public virtual Task<ConstructResponse> Construct(ConstructRequest request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(Construct)}' is not implemented ");
        }

        public virtual Task<CallResponse> Call(CallRequest request, CancellationToken ct)
        {
            throw new NotImplementedException($"The method '{nameof(Call)}' is not implemented ");
        }

        public static Task Serve(string[] args, string? version, Func<IHost, Provider> factory, System.Threading.CancellationToken cancellationToken)
        {
            return Serve(args, version, factory, cancellationToken, System.Console.Out);
        }

        public static async Task Serve(string[] args, string? version, Func<IHost, Provider> factory, System.Threading.CancellationToken cancellationToken, System.IO.TextWriter stdout)
        {
            // maxRpcMessageSize raises the gRPC Max message size from `4194304` (4mb) to `419430400` (400mb)
            var maxRpcMessageSize = 400 * 1024 * 1024;

            var host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .ConfigureKestrel(kestrelOptions =>
                        {
                            kestrelOptions.Listen(IPAddress.Loopback, 0, listenOptions =>
                            {
                                listenOptions.Protocols = HttpProtocols.Http2;
                            });
                        })
                        .ConfigureAppConfiguration((context, config) =>
                        {
                            // clear so we don't read appsettings.json
                            // note that we also won't read environment variables for config
                            config.Sources.Clear();

                            var memConfig = new Dictionary<string, string>();
                            if (args.Length > 0)
                            {
                                memConfig.Add("Host", args[0]);
                            }
                            if (version != null)
                            {
                                memConfig.Add("Version", version);
                            }
                            config.AddInMemoryCollection(memConfig);
                        })
                        .ConfigureLogging(loggingBuilder =>
                        {
                            // disable default logging
                            loggingBuilder.ClearProviders();
                        })
                        .ConfigureServices(services =>
                        {
                            // to be injected into ResourceProviderService
                            services.AddSingleton(factory);
                            services.AddSingleton<ResourceProviderService>();

                            services.AddGrpc(grpcOptions =>
                            {
                                grpcOptions.MaxReceiveMessageSize = maxRpcMessageSize;
                                grpcOptions.MaxSendMessageSize = maxRpcMessageSize;
                            });
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapGrpcService<ResourceProviderService>();
                            });
                        });
                })
                .Build();

            // before starting the host, set up this callback to tell us what port was selected
            var portTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var portRegistration = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(() =>
            {
                try
                {
                    var serverFeatures = host.Services.GetRequiredService<IServer>().Features;
                    var addressesFeature = serverFeatures.Get<IServerAddressesFeature>();
                    Debug.Assert(addressesFeature != null, "Server should have an IServerAddressesFeature");
                    var addresses = addressesFeature.Addresses.ToList();
                    Debug.Assert(addresses.Count == 1, "Server should only be listening on one address");
                    var uri = new Uri(addresses[0]);
                    portTcs.TrySetResult(uri.Port);
                }
                catch (Exception ex)
                {
                    portTcs.TrySetException(ex);
                }
            });

            await host.StartAsync(cancellationToken);

            var port = await portTcs.Task;
            // Explicitly write just the number and "\n". WriteLine would write "\r\n" on Windows, and while
            // the engine has now been fixed to handle that (see https://github.com/pulumi/pulumi/pull/11915)
            // we work around this here so that old engines can use dotnet providers as well.
            stdout.Write(port.ToString() + "\n");

            await host.WaitForShutdownAsync(cancellationToken);

            host.Dispose();
        }
    }
}
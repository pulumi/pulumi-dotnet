using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pulumi.Testing;
using Pulumi.Tests.Logging;
using Xunit;

namespace Pulumi.Tests.Provider;

public abstract class ProviderServerTestHost : IAsyncLifetime
{
    private IHost? host;
    private GrpcChannel? channel;
    private TcpListener? engineListener;
    private TcpListener? monitoringListener;

    public IHost Host => host ?? throw new InvalidOperationException("Host is not running");
    public GrpcChannel Channel => channel ?? throw new InvalidOperationException("Host is not running");
    public ILoggerProvider? LoggerProvider { get; set; }

    public async Task InitializeAsync()
    {
        var tcpListener = System.Net.Sockets.TcpListener.Create(0);
        var localEndpoint = tcpListener.LocalEndpoint.ToString();
        if (localEndpoint == null)
        {
            throw new InvalidOperationException("TcpListener did not bind to a local endpoint.");
        }

        var args = new[] { localEndpoint };

        var cts = new System.Threading.CancellationTokenSource();

        host = Experimental.Provider.Provider.BuildHost(args, "1.0", new MockDeploymentBuilder(), BuildProvider,
            builder => builder.ConfigureServices(services =>
                services.AddLogging(loggingBuilder =>
                    loggingBuilder.AddProvider(new DelegatedLoggerProvider(() => LoggerProvider)))));
        await host.StartAsync(cts.Token);

        // Grab the uri from the host
        var hostUri = Experimental.Provider.Provider.GetHostUri(host);

        // Inititialize the engine channel once for this address
        channel = GrpcChannel.ForAddress(hostUri, new GrpcChannelOptions
        {
            Credentials = Grpc.Core.ChannelCredentials.Insecure,
        });
    }

    protected abstract Experimental.Provider.Provider BuildProvider(Experimental.Provider.IHost runner);

    public async Task DisposeAsync()
    {
        if (channel != null)
        {
            await channel.ShutdownAsync();
            channel.Dispose();
            channel = null;
        }

        if (host != null)
        {
            await host.StopAsync();
            host.Dispose();
            host = null;
        }

        if (engineListener != null)
        {
            engineListener.Stop();
            engineListener = null;
        }

        if (monitoringListener != null)
        {
            monitoringListener.Stop();
            monitoringListener = null;
        }
    }
}

public class ProviderServerTestHost<TProvider> : ProviderServerTestHost where TProvider : Experimental.Provider.Provider
{
    protected override Experimental.Provider.Provider BuildProvider(Experimental.Provider.IHost runner)
    {
        return Activator.CreateInstance<TProvider>() ??
               throw new InvalidOperationException($"Unable to create instance of type '{typeof(TProvider).Name}'.");
    }
}

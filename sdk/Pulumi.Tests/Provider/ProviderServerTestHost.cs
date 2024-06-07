using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Pulumi.Tests.Provider;

public abstract class ProviderServerTestHost : IAsyncLifetime
{
    private IHost? host;
    private GrpcChannel? channel;

    public IHost Host => host ?? throw new InvalidOperationException("Host is not running");
    public GrpcChannel Channel => channel ?? throw new InvalidOperationException("Host is not running");

    public async Task InitializeAsync()
    {
        var tcpListener = System.Net.Sockets.TcpListener.Create(0);
        var args = new string[] { tcpListener.LocalEndpoint.ToString() };

        var cts = new System.Threading.CancellationTokenSource();

        // Custom stdout so we can see what port Serve chooses
        host = Experimental.Provider.Provider.BuildHost(args, "1.0", BuildProvider);
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
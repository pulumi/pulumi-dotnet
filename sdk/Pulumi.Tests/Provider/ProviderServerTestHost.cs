using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Pulumi.Tests.Provider;

public class ProviderServerTestHost<TProvider> : IAsyncLifetime where TProvider : Experimental.Provider.Provider
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
        host = Experimental.Provider.Provider.BuildHost(args, "1.0",
            _ => Activator.CreateInstance<TProvider>() ??
                 throw new InvalidOperationException($"Unable to create instance of type '{typeof(TProvider).Name}'."));
        await host.StartAsync(cts.Token);

        // Grab the uri from the host
        var hostUri = Experimental.Provider.Provider.GetHostUri(host);

        // Inititialize the engine channel once for this address
        channel = GrpcChannel.ForAddress(hostUri, new GrpcChannelOptions
        {
            Credentials = Grpc.Core.ChannelCredentials.Insecure,
        });
    }

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
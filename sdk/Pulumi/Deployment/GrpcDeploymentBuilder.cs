namespace Pulumi;

internal class GrpcDeploymentBuilder : IDeploymentBuilder
{
    public static GrpcDeploymentBuilder Instance = new();

    private GrpcDeploymentBuilder()
    {
    }

    public IEngine BuildEngine(string engineAddress)
    {
        return new GrpcEngine(engineAddress);
    }

    public IMonitor BuildMonitor(string monitoringEndpoint)
    {
        return new GrpcMonitor(monitoringEndpoint);
    }
}

namespace Pulumi;

internal interface IDeploymentBuilder
{
    public IEngine BuildEngine(string engineAddress);
    public IMonitor BuildMonitor(string monitoringEndpoint);
}

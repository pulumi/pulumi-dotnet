namespace Pulumi;

internal interface IDeploymentBuilder
{
    public Experimental.IEngine BuildEngine(string engineAddress);
    public IMonitor BuildMonitor(string monitoringEndpoint);
}

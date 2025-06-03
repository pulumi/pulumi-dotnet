namespace Pulumi.Testing;

internal class MockDeploymentBuilder : IDeploymentBuilder
{
    private readonly Experimental.IEngine? engine;
    private readonly IMonitor? monitor;

    public MockDeploymentBuilder(Experimental.IEngine? engine = default, IMonitor? monitor = default)
    {
        this.engine = engine;
        this.monitor = monitor;
    }

    public Experimental.IEngine BuildEngine(string engineAddress)
    {
        return engine ?? new MockEngine();
    }

    public IMonitor BuildMonitor(string monitoringEndpoint)
    {
        return monitor ?? new MockMonitor(new EmptyMocks());
    }
}

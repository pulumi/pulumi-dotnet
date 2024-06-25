namespace Pulumi.Testing;

internal class MockDeploymentBuilder : IDeploymentBuilder
{
    private readonly IEngine? engine;
    private readonly IMonitor? monitor;

    public MockDeploymentBuilder(IEngine? engine = default, IMonitor? monitor = default)
    {
        this.engine = engine;
        this.monitor = monitor;
    }

    public IEngine BuildEngine(string engineAddress)
    {
        return engine ?? new MockEngine();
    }

    public IMonitor BuildMonitor(string monitoringEndpoint)
    {
        return monitor ?? new MockMonitor(new EmptyMocks());
    }
}

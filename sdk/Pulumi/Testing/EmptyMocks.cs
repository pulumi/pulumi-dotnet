using System.Threading.Tasks;

namespace Pulumi.Testing;

public class EmptyMocks : IMocks
{
    public Task<(string? id, object state)> NewResourceAsync(MockResourceArgs args)
    {
        return Task.FromResult<(string? id, object state)>((null, args.Inputs));
    }

    public Task<object> CallAsync(MockCallArgs args)
    {
        return Task.FromResult<object>(args);
    }
}

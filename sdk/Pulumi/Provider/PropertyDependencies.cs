using System.Collections.Generic;

namespace Pulumi.Experimental.Provider;

public sealed class PropertyDependencies
{
    public readonly ISet<string> Urns;

    public PropertyDependencies(ISet<string> urns)
    {
        Urns = urns;
    }
}

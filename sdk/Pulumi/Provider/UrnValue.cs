using System;

namespace Pulumi.Experimental.Provider;

public record struct UrnValue(string Value)
{
    public static implicit operator string(UrnValue value)
    {
        return value.Value;
    }
}

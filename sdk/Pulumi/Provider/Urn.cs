using System;

namespace Pulumi.Experimental.Provider;

public record struct Urn(string Value)
{
    public static implicit operator string(Urn value)
    {
        return value.Value;
    }
}

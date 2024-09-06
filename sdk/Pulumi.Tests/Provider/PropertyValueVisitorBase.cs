using System.Collections.Generic;
using System.Collections.Immutable;
using Pulumi.Experimental.Provider;
using Xunit;

namespace Pulumi.Tests.Provider;

public abstract class PropertyValueVisitorBase
{
    public void Visit(PropertyValue propertyValue)
    {
        if (propertyValue.TryGetArchive(out var archive))
        {
            VisitArchive(propertyValue, archive);

            return;
        }

        if (propertyValue.TryGetArray(out var array))
        {
            VisitArray(propertyValue, array);

            return;
        }

        if (propertyValue.TryGetAsset(out var asset))
        {
            VisitAsset(propertyValue, asset);

            return;
        }

        if (propertyValue.TryGetBool(out var boolValue))
        {
            VisitBool(propertyValue, boolValue);

            return;
        }

        if (propertyValue.TryGetNumber(out var number))
        {
            VisitNumber(propertyValue, number);

            return;
        }

        if (propertyValue.TryGetString(out var stringValue))
        {
            VisitString(propertyValue, stringValue);

            return;
        }

        if (propertyValue.TryGetObject(out var obj))
        {
            VisitObject(propertyValue, obj);

            return;
        }

        if (propertyValue.TryGetSecret(out var secret))
        {
            VisitSecret(propertyValue, secret);

            return;
        }

        if (propertyValue.TryGetOutput(out var output))
        {
            VisitOutput(propertyValue, output);
        }
    }

    protected virtual void VisitString(PropertyValue propertyValue, string stringValue)
    {
    }

    protected virtual void VisitNumber(PropertyValue propertyValue, double number)
    {
    }

    protected virtual void VisitBool(PropertyValue propertyValue, bool boolValue)
    {
    }

    protected virtual void VisitAsset(PropertyValue propertyValue, Asset? value)
    {
    }

    protected virtual void VisitArchive(PropertyValue propertyValue, Archive? archive)
    {
    }

    protected virtual void VisitOutput(PropertyValue propertyValue, OutputReference output)
    {
        if (output.Value != null)
        {
            Visit(output.Value);
        }
    }

    protected virtual void VisitSecret(PropertyValue propertyValue, PropertyValue secret)
    {
        Visit(secret);
    }

    protected virtual void VisitObject(PropertyValue propertyValue, ImmutableDictionary<string, PropertyValue> obj)
    {
        foreach (var value in obj.Values)
        {
            Visit(value);
        }
    }

    protected virtual void VisitArray(PropertyValue propertyValue, ImmutableArray<PropertyValue> array)
    {
        foreach (var value in array)
        {
            Visit(value);
        }
    }
}

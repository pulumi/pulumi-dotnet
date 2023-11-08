using System;

namespace Pulumi.Experimental.Provider.Serialization;


/// <summary>
/// The specified converter type must derive from PropertyValueConverter.
///
/// When placed on a property, the specified converter will always be used.
///
/// When placed on a type, the specified converter will be used unless a compatible converter is added to the
/// PropertyValueSerializerOptions.Converters collection or there is another PropertyValueConverterAttribute on a
/// property of the same type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Field | AttributeTargets.Interface | AttributeTargets.Property | AttributeTargets.Struct, AllowMultiple = false)]
public partial class PropertyValueConverterAttribute : Attribute
{
    public PropertyValueConverterAttribute(Type converterType)
    {
        ConverterType = converterType;
    }
    public Type ConverterType { get; init; }
}

using System;
using System.Threading.Tasks;

namespace Pulumi.Experimental.Provider.Serialization;

public abstract class PropertyValueConverter
{
    // We don't actually want anyone writing their own PropertyValueConverters.
    internal PropertyValueConverter() { }

    internal abstract bool CanConvert(Type type);

    internal abstract PropertySchema GetSchema(Type type);

    internal abstract object? ReadAsObject(PropertyValue value);
    internal abstract Task<PropertyValue> WriteAsObject(object? value);
}

public abstract class PropertyValueConverterFactory : PropertyValueConverter
{
    internal override object? ReadAsObject(PropertyValue value)
    {
        throw new NotImplementedException("ReadAsObject should not be called on a PropertyValueConverterFactory");
    }

    internal override Task<PropertyValue> WriteAsObject(object? value)
    {
        throw new NotImplementedException("WriteAsObject should not be called on a PropertyValueConverterFactory");
    }

    /// <summary>
    /// Creates a converter for a specified type.
    /// </summary>
    public abstract PropertyValueConverter CreateConverter(Type type, PropertyValueSerializerOptions options);
}

public abstract class PropertyValueConverter<T> : PropertyValueConverter
{

    internal override object? ReadAsObject(PropertyValue value)
    {
        return Read(value);
    }

    internal override Task<PropertyValue> WriteAsObject(object? value)
    {
        return Write((T)value!);
    }

    public abstract T Read(PropertyValue value);
    public abstract Task<PropertyValue> Write(T value);
}

public abstract class CustomPropertyValueConverter<T> : PropertyValueConverter<T>
{

    readonly Lazy<PropertyValueConverter<T>> _converter;

    protected CustomPropertyValueConverter()
    {
        _converter = new Lazy<PropertyValueConverter<T>>(CreateConverter);
    }

    public abstract PropertyValueConverter<T> CreateConverter();

    internal override bool CanConvert(Type type) => _converter.Value.CanConvert(type);
    internal override PropertySchema GetSchema(Type type) => _converter.Value.GetSchema(type);
    public override T Read(PropertyValue value) => _converter.Value.Read(value);
    public override Task<PropertyValue> Write(T value) => _converter.Value.Write(value);
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using Humanizer;

namespace Pulumi.Experimental.Provider.Serialization;

sealed class ReflectionConverter<T> : PropertyValueConverter<T>
{
    Dictionary<string, PropertyValueConverter> _converters = new Dictionary<string, PropertyValueConverter>();


    public ReflectionConverter(PropertyValueSerializerOptions options)
    {
        // Assume this is an object type
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var property in properties)
        {
            var propertyType = property.PropertyType;
            var converter = options.GetConverter(propertyType);
            _converters.Add(property.Name, converter);
        }
    }

    internal override bool CanConvert(Type type)
    {
        return type == typeof(T);
    }

    private string PropertyName(PropertyInfo property, Type declaringType)
    {
        var propertyName = CamelCase(property.Name);
        var inputAttribute = property.GetCustomAttribute<InputAttribute>();

        if (inputAttribute == null && TryGetBackingField(propertyName, out var inputField))
        {
            inputAttribute = inputField.GetCustomAttribute<InputAttribute>();
        }

        if (inputAttribute != null && !string.IsNullOrWhiteSpace(inputAttribute.Name))
        {
            propertyName = inputAttribute.Name;
        }

        var outputAttribute = property.GetCustomAttribute<OutputAttribute>();

        if (outputAttribute != null && !string.IsNullOrWhiteSpace(outputAttribute.Name))
        {
            propertyName = outputAttribute.Name;
        }

        return propertyName;

        bool TryGetBackingField(string propName, [NotNullWhen(true)] out FieldInfo? field)
        {
            field = declaringType.GetField($"_{CamelCase(propName)}",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            return field != null;
        }
    }

    string CamelCase(string input) =>
        input.Length > 1
            ? input.Substring(0, 1).ToLowerInvariant() + input.Substring(1)
            : input.ToLowerInvariant();

    internal override PropertySchema GetSchema(Type type)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var schema = new PropertySchema(PropertyType.Object);
        foreach (var property in properties)
        {
            var subschema = _converters[property.Name].GetSchema(property.PropertyType);
            schema.Fields.Add(PropertyName(property, type), subschema);
        }
        return schema;
    }

    public override T Read(PropertyValue value)
    {
        if (value.TryGetObject(out var obj))
        {
            var type = typeof(T);
            var result = Activator.CreateInstance(type);

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                var propertyName = PropertyName(property, type);
                var inner = obj[propertyName];
                var converter = _converters[property.Name];
                var innerValue = converter.ReadAsObject(inner);
                property.SetValue(result, innerValue);
            }

            return (T)result!;
        }
        else
        {
            throw new InvalidOperationException("Expected object");
        }
    }

    public override async Task<PropertyValue> Write(T value)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var dict = new Dictionary<string, PropertyValue>();
        foreach (var property in properties)
        {
            var innerValue = property.GetValue(value);
            var converter = _converters[property.Name];
            var innerProperty = await converter.WriteAsObject(innerValue);
            var propertyName = PropertyName(property, typeof(T));
            dict.Add(propertyName, innerProperty);
        }
        return new PropertyValue(ImmutableDictionary.CreateRange(dict));
    }
}

sealed class AnyConverter : PropertyValueConverter<object?>
{
    internal override bool CanConvert(Type type)
    {
        return type == typeof(object);
    }

    public override object? Read(PropertyValue value)
    {
        if (value.IsNull)
        {
            return null;
        }
        else if (value.TryGetString(out var str))
        {
            return str;
        }
        else if (value.TryGetNumber(out var number))
        {
            return number;
        }
        else if (value.TryGetBool(out var boolean))
        {
            return boolean;
        }
        else if (value.TryGetArray(out var array))
        {
            var result = new object?[array.Length];
            for (var i = 0; i < array.Length; ++i)
            {
                result[i] = this.Read(array[i]);
            }
            return result;
        }
        throw new InvalidOperationException("Expected any got " + value.Type);
    }

    public override async Task<PropertyValue> Write(object? value)
    {
        if (value is null)
        {
            return PropertyValue.Null;
        }
        if (value is string str)
        {
            return new PropertyValue(str);
        }
        else if (value is bool boolean)
        {
            return new PropertyValue(boolean);
        }
        else if (value is double number)
        {
            return new PropertyValue(number);
        }
        else if (value is Array array)
        {
            var length = array.GetLength(0);
            var builder = ImmutableArray.CreateBuilder<PropertyValue>(length);
            for (var i = 0; i < length; ++i)
            {
                builder.Add(await this.Write(array.GetValue(i)));
            }
            return new PropertyValue(builder.ToImmutable());
        }
        throw new InvalidOperationException("Expected any got " + value.GetType());
    }

    internal override PropertySchema GetSchema(Type type)
    {
        return new PropertySchema(PropertyType.Any);
    }
}

sealed class NullableConverterFactory : PropertyValueConverterFactory
{
    internal override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    public override PropertyValueConverter CreateConverter(Type typeToConvert, PropertyValueSerializerOptions options)
    {
        var valueTypeToConvert = typeToConvert.GetGenericArguments()[0];
        var valueConverter = options.GetConverter(valueTypeToConvert);

        throw new NotImplementedException("CreateConverter: NullableConverterFactory");
    }

    internal override PropertySchema GetSchema(Type type)
    {
        throw new NotImplementedException("GetSchema: NullableConverterFactory");
    }
}

sealed class StringConverter : PropertyValueConverter<string>
{
    internal override bool CanConvert(Type type)
    {
        return type == typeof(string);
    }

    public override string Read(PropertyValue value)
    {
        return value.TryGetString(out var str) ? str : throw new InvalidOperationException("Expected string");
    }

    public override Task<PropertyValue> Write(string value)
    {
        return Task.FromResult(new PropertyValue(value));
    }


    internal override PropertySchema GetSchema(Type type)
    {
        return new PropertySchema(PropertyType.String);
    }
}


sealed class MapConverter<T, U> : PropertyValueConverter<T>
{

    PropertyValueConverter<U> converter;

    Func<U, T> read;
    Func<T, U> write;

    public MapConverter(
        PropertyValueConverter<U> converter,
        Func<U, T> read,
        Func<T, U> write
    )
    {
        this.converter = converter;
        this.read = read;
        this.write = write;
    }


    internal override bool CanConvert(Type type)
    {
        return type == typeof(T);
    }

    public override T Read(PropertyValue value)
    {
        return read(converter.Read(value));
    }

    public override async Task<PropertyValue> Write(T value)
    {
        return await converter.Write(write(value));
    }

    internal override PropertySchema GetSchema(Type type)
    {
        return converter.GetSchema(type);
    }
}

enum PropertyType
{
    Any,
    String,
    Number,
    Boolean,
    Object,
}

sealed class PropertySchema
{
    public PropertyType Type { get; }
    public bool Required { get; }
    public PropertySchema(PropertyType type, bool required = true)
    {
        Type = type;
        Required = required;
    }

    public Dictionary<string, PropertySchema> Fields { get; } = new Dictionary<string, PropertySchema>();

    public static ResourceSpec ToResourceSpec(PropertySchema inputs, PropertySchema outputs)
    {
        var inputProperties = new Dictionary<string, PropertySpec>();
        var requiredInputs = new HashSet<string>();
        var properties = new Dictionary<string, PropertySpec>();
        var required = new HashSet<string>();

        foreach (var field in inputs.Fields)
        {
            var fieldName = field.Key;
            var fieldSchema = field.Value;

            if (fieldSchema.Required)
            {
                requiredInputs.Add(fieldName);
            }

            inputProperties.Add(fieldName, ToPropertySpec(fieldSchema));
        }

        foreach (var field in outputs.Fields)
        {
            var fieldName = field.Key;
            var fieldSchema = field.Value;

            if (fieldSchema.Required)
            {
                required.Add(fieldName);
            }

            properties.Add(fieldName, ToPropertySpec(fieldSchema));
        }

        return new ResourceSpec(inputProperties, requiredInputs, properties, required, false);
    }

    public static PropertySpec ToPropertySpec(PropertySchema schema)
    {
        return schema.Type switch
        {
            PropertyType.Any => PropertySpec.CreateReference("pulumi.json#/Any"),
            PropertyType.String => PropertySpec.String,
            _ => throw new NotImplementedException("ToPropertySpec: " + schema.Type)
        };
    }
}

public static class HList
{
    public readonly static HNil Nil = new HNil();
}

public class HList<A> where A : HList<A> { }

public class HNil : HList<HNil>
{


    public HCons<U, HNil> Extend<U>(U head)
    {
        return new HCons<U, HNil>(head, this);
    }
}


public class HCons<H, T> : HList<HCons<H, T>> where T : HList<T>
{
    public H Head;
    public T Tail;

    public HCons(H head, T tail)
    {
        this.Head = head;
        this.Tail = tail;
    }

    public HCons<U, HCons<H, T>> Extend<U>(U head)
    {
        return new HCons<U, HCons<H, T>>(head, this);
    }
}

public abstract class TList<T, H> where T : TList<T, H> where H : HList<H>
{
    public abstract H Read(ImmutableDictionary<string, PropertyValue> values);

    public abstract Task<ImmutableDictionary<string, PropertyValue>> Write(H values);
}

public class TNil : TList<TNil, HNil>
{

    public override HNil Read(ImmutableDictionary<string, PropertyValue> values)
    {
        if (!values.IsEmpty)
        {
            throw new Exception("expected values to be empty");
        }
        return new HNil();
    }

    public override Task<ImmutableDictionary<string, PropertyValue>> Write(HNil values)
    {
        return Task.FromResult(ImmutableDictionary<string, PropertyValue>.Empty);
    }

    public TCons<U, TNil, HNil> Extend<U>(string name, PropertyValueConverter<U> converter)
    {
        return new TCons<U, TNil, HNil>(name, converter, this);
    }
}

public class TCons<T, A, H> : TList<TCons<T, A, H>, HCons<T, H>>
    where A : TList<A, H>
    where H : HList<H>
{
    public string Name;
    public PropertyValueConverter<T> Converter;
    public TList<A, H> Tail;

    public TCons(string name, PropertyValueConverter<T> converter, TList<A, H> tail)
    {
        this.Name = name;
        this.Converter = converter;
        this.Tail = tail;
    }

    public override HCons<T, H> Read(ImmutableDictionary<string, PropertyValue> values)
    {
        if (values.TryGetValue(Name, out var propertyValue))
        {
            var read = Converter.Read(propertyValue);
            return new HCons<T, H>(read, Tail.Read(values.Remove(Name)));
        }
        throw new Exception("could not find property " + Name);
    }

    public override async Task<ImmutableDictionary<string, PropertyValue>> Write(HCons<T, H> values)
    {
        var properties = await Tail.Write(values.Tail);
        var property = await Converter.Write(values.Head);
        return properties.Add(Name, property);
    }

    public TCons<U, TCons<T, A, H>, HCons<T, H>> Extend<U>(string name, PropertyValueConverter<U> converter)
    {
        return new TCons<U, TCons<T, A, H>, HCons<T, H>>(name, converter, this);
    }

    public PropertyValueConverter<HCons<T, H>> ToConverter()
    {
        return new ObjectConverter<TCons<T, A, H>, HCons<T, H>>(this);
    }
}


sealed class ObjectConverter<T, H> : PropertyValueConverter<H>
    where T : TList<T, H>
    where H : HList<H>
{
    TList<T, H> list;

    public ObjectConverter(TList<T, H> fields)
    {
        list = fields;
    }

    internal override bool CanConvert(Type type)
    {
        return type == typeof(string);
    }

    public override H Read(PropertyValue value)
    {
        if (value.TryGetObject(out var obj))
        {
            return list.Read(obj);
        }
        else
        {
            throw new InvalidOperationException("Expected object");
        }
    }

    public override async Task<PropertyValue> Write(H value)
    {
        var obj = await list.Write(value);
        return new PropertyValue(obj);
    }

    internal override PropertySchema GetSchema(Type type)
    {
        var schema = new PropertySchema(PropertyType.String);
        return schema;
    }
}


public class PropertyValueSerializerOptions
{
    public static PropertyValueSerializerOptions Default { get; } = new PropertyValueSerializerOptions();

    /// <summary>
    /// Gets the list of user-defined converters that were registered.
    /// </summary>
    public IList<PropertyValueConverter> Converters { get; } = new List<PropertyValueConverter>();

    PropertyValueConverter? getConverterFromList(Type typeToConvert)
    {
        foreach (var converter in Converters)
        {
            if (converter.CanConvert(typeToConvert))
            {
                return converter;
            }
        }

        return null;
    }

    static TAttribute? getUniqueCustomAttribute<TAttribute>(System.Reflection.MemberInfo memberInfo, bool inherit)
        where TAttribute : Attribute
    {
        object[] attributes = memberInfo.GetCustomAttributes(typeof(TAttribute), inherit);

        if (attributes.Length == 0)
        {
            return null;
        }

        if (attributes.Length == 1)
        {
            return (TAttribute)attributes[0];
        }

        throw new Exception($"Multiple {typeof(TAttribute).Name} attributes found on {memberInfo.Name}");
    }

    static PropertyValueConverter GetConverterFromAttribute(PropertyValueConverterAttribute converterAttribute, Type typeToConvert, System.Reflection.MemberInfo? memberInfo, PropertyValueSerializerOptions options)
    {
        PropertyValueConverter? converter;

        Type declaringType = memberInfo?.DeclaringType ?? typeToConvert;
        Type converterType = converterAttribute.ConverterType;

        System.Reflection.ConstructorInfo? ctor = converterType.GetConstructor(Type.EmptyTypes);
        if (!typeof(PropertyValueConverter).IsAssignableFrom(converterType) || ctor == null || !ctor.IsPublic)
        {
            throw new Exception($"The specified type {converterType} does not derive from {nameof(PropertyValueConverter)} or does not have a public parameterless constructor.");
        }

        converter = (PropertyValueConverter)Activator.CreateInstance(converterType)!;


        if (!converter.CanConvert(typeToConvert))
        {
            Type? underlyingType = Nullable.GetUnderlyingType(typeToConvert);
            if (underlyingType != null && converter.CanConvert(underlyingType))
            {
                if (converter is PropertyValueConverterFactory converterFactory)
                {
                    converter = converterFactory.CreateConverter(underlyingType, options);
                    if (converter == null)
                    {
                        throw new Exception($"The specified converter type {converterType} does not support the type {typeToConvert}.");
                    }
                    if (converter is not PropertyValueConverterFactory)
                    {
                        throw new Exception($"The specified converter type {converterType} does not derive from {nameof(PropertyValueConverterFactory)}.");
                    }
                }

                // Allow nullable handling to forward to the underlying type's converter.
                //return NullableConverterFactory.CreateValueConverter(underlyingType, converter);
                throw new NotImplementedException("GetConverterFromAttribute: NullableConverterFactory");
            }

            throw new Exception($"The specified converter type {converterType} does not support the type {typeToConvert}.");
        }

        return converter;
    }

    PropertyValueConverter getBuiltInConverter(Type type)
    {
        if (type == typeof(string))
        {
            return new StringConverter();
        }
        if (type == typeof(object))
        {
            return new AnyConverter();
        }

        return (PropertyValueConverter)Activator.CreateInstance(
            typeof(ReflectionConverter<>).MakeGenericType(type),
            new object?[] { this }
        )!;
    }

    /// <summary>
    /// Returns the converter for the specified type.
    /// </summary>
    /// <param name="typeToConvert">The type to return a converter for.</param>
    /// <returns>The first converter that supports the given type, or null if there is no converter.</returns>
    public PropertyValueConverter GetConverter(Type typeToConvert)
    {
        // Priority 1: Attempt to get custom converter from the Converters list.
        PropertyValueConverter? converter = getConverterFromList(typeToConvert);

        // Priority 2: Attempt to get converter from [JsonConverter] on the type being converted.
        if (converter == null)
        {
            var converterAttribute = getUniqueCustomAttribute<PropertyValueConverterAttribute>(typeToConvert, inherit: false);
            if (converterAttribute != null)
            {
                converter = GetConverterFromAttribute(converterAttribute, typeToConvert: typeToConvert, memberInfo: null, this);
            }
        }

        // Priority 3: Query the built-in converters.
        converter ??= getBuiltInConverter(typeToConvert);

        // Expand if factory converter
        if (converter is PropertyValueConverterFactory factory)
        {
            converter = factory.CreateConverter(typeToConvert, this);
        }

        return converter;
    }
}

public sealed class PropertyValueSerialiser
{
    public static object? Deserialize(PropertyValue value, Type returnType, PropertyValueSerializerOptions? options = null)
    {
        options ??= PropertyValueSerializerOptions.Default;
        var converter = options.GetConverter(returnType);
        return converter.ReadAsObject(value);
    }

    public static T? Deserialize<T>(PropertyValue value, PropertyValueSerializerOptions? options = null)
    {
        return (T?)Deserialize(value, typeof(T), options);
    }

    public static Task<PropertyValue> Serialize(object? value, PropertyValueSerializerOptions? options = null)
    {
        options ??= PropertyValueSerializerOptions.Default;
        var converter = options.GetConverter(value?.GetType() ?? typeof(object));
        return converter.WriteAsObject(value);
    }
}

public static class Serialiser
{
    public static PropertyValueConverter<float> Float()
    {
        throw new Exception();
    }

    public static PropertyValueConverter<string> String()
    {
        return new StringConverter();
    }

    public static PropertyValueConverter<T> Map<T, U>(
        PropertyValueConverter<U> converter,
        Func<U, T> read,
        Func<T, U> write
    )
    {
        return new MapConverter<T, U>(converter, read, write);
    }

    public static PropertyValueConverter<(T0, T1)> Object<T0, T1>(
        string n0, PropertyValueConverter<T0> s0,
        string n1, PropertyValueConverter<T1> s1
    )
    {
        var converter = new TNil()
            .Extend(n0, s0)
            .Extend(n1, s1)
            .ToConverter();

        return Map(converter,
            list => (list.Tail.Head, list.Head),
            tuple => new HNil().Extend(tuple.Item1).Extend(tuple.Item2)
        );
    }
}

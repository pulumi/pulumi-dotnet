using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Pulumi.Serialization;

namespace Pulumi.Analyzer
{
    [Obsolete("PropertyValueSerializer is highly experimental, and the shape of the API may be removed or changed at any time. Use at your own risk")]
    public class PropertyValueSerializer
    {
        string CamelCase(string input) =>
            input.Length > 1
                ? string.Concat(input.Substring(0, 1).ToLowerInvariant(), input.AsSpan(1))
                : input.ToLowerInvariant();

        private object? DeserializeObject(ImmutableDictionary<string, PropertyValue> inputs, Type targetType, string[] path)
        {
            var properties = targetType.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            var objectShape = properties.Select(property => new PropInfo(property, PropertyName(property, targetType))).ToArray();

            var candidateConstructors = new List<ConstructorInfo>();
            ConstructorInfo? parameterlessConstructor = null;
            foreach (var constructor in targetType.GetConstructors())
            {
                var parameters = constructor.GetParameters();
                if (parameters.Length == 0)
                {
                    parameterlessConstructor = constructor;
                    continue;
                }

                // parameters match the shape of the type when all the parameters
                // have a corresponding property with the same name and type
                var parametersMatchShape = parameters.All(param =>
                {
                    return objectShape.Any(property =>
                        param.Name == CamelCase(property.Name) &&
                        param.ParameterType == property.PropertyType);
                });

                // the shape of the type matches the parameters of the constructor
                // when all the properties either have a corresponding parameter
                // or doesn't have a corresponding parameter but is nullable
                var shapeMatchesParameters = objectShape.All(property =>
                {
                    foreach (var param in parameters)
                    {
                        if (param.Name == CamelCase(property.Name) &&
                            param.ParameterType == property.PropertyType)
                        {
                            return true;
                        }
                    }

                    return IsNullable(property.PropertyType);
                });

                if (parametersMatchShape && shapeMatchesParameters)
                {
                    candidateConstructors.Add(constructor);
                }
            }

            var constructorWithMatchingParameters =
                candidateConstructors
                    .FirstOrDefault(ctor =>
                    {
                        var parameters = ctor.GetParameters();
                        var parameterCountMatches = parameters.Length == inputs.Count;
                        var parameterNamesMatch = parameters.All(param =>
                        {
                            var parameterName = param.Name ?? "";
                            return inputs.ContainsKey(parameterName);
                        });

                        return parameterCountMatches && parameterNamesMatch;
                    });

            if (constructorWithMatchingParameters != null)
            {
                var parameters = constructorWithMatchingParameters.GetParameters();
                var deserializedParameters = parameters.Select(parameter =>
                {
                    var parameterName = parameter.Name ?? "";
                    var parameterValue = inputs.GetValueOrDefault(parameterName, PropertyValue.Null);
                    var parameterPath = path.Append($"param({parameterName})").ToArray();
                    var value = DeserializeValue(parameterValue, parameter.ParameterType, parameterPath);
                    if (value == null && !IsNullable(parameter.ParameterType))
                    {
                        throw new InvalidOperationException(
                            $"Could not deserialize parameter {parameterName} of type {parameter.ParameterType.Name} " +
                            $"at path [{string.Join(", ", parameterPath)}]");
                    }

                    return value;
                });

                return constructorWithMatchingParameters.Invoke(deserializedParameters.ToArray());
            }

            object? instance =
                parameterlessConstructor != null
                    ? parameterlessConstructor.Invoke(Array.Empty<object>())
                    : Activator.CreateInstance(targetType);

            foreach (var property in objectShape)
            {
                var propertyPath = path.Append(property.Name).ToArray();
                if (inputs.TryGetValue(property.Name, out var value))
                {
                    var deserializedValue = DeserializeValue(value, property.PropertyType, propertyPath);
                    property.SetValue(instance, deserializedValue);
                }
                else
                {
                    var maybeDeserializedEmptyValue = DeserializeValue(PropertyValue.Null, property.PropertyType, propertyPath);
                    // we couldn't find the corresponding property value in the inputs given
                    // that is alright if the property is optional (i.e. the type is nullable)
                    // otherwise we throw an error
                    if (!IsNullable(property.PropertyType) && maybeDeserializedEmptyValue == null)
                    {
                        // TODO: implement a proper exception type that includes the type info, errors and property value
                        var errorPath = "[" + string.Join(", ", propertyPath) + "]";
                        throw new InvalidOperationException(
                            $"Could not deserialize object of type {targetType.Name}, missing required property {property.Name} at {errorPath}");
                    }

                    property.SetValue(instance, maybeDeserializedEmptyValue);
                }
            }

            return instance;
        }

        private sealed record PropInfo
        {
            private readonly PropertyInfo _property;

            public PropInfo(PropertyInfo property, string name)
            {
                Name = name;
                _property = property;
            }

            public Type PropertyType => _property.PropertyType;

            public void SetValue(object? obj, object? value) => _property.SetValue(obj, value);

            public string Name { get; set; }

        }

        private bool IsNullable(Type type)
        {
            return !type.IsValueType || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        private string PropertyName(PropertyInfo property, Type declaringType)
        {
            var propertyName = property.Name;
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

        public async Task<PropertyValue> Serialize<T>(T value)
        {
            if (value == null)
            {
                return PropertyValue.Null;
            }

            var targetType = value.GetType();
            switch (value)
            {
                case string stringValue:
                    return new PropertyValue(stringValue);
                case int integer:
                    return new PropertyValue(integer);
                case bool boolean:
                    return new PropertyValue(boolean);
                case double number:
                    return new PropertyValue(number);
                case float number:
                    return new PropertyValue(number);
                case decimal number:
                    return new PropertyValue(Convert.ToDouble(number));
                case byte number:
                    return new PropertyValue(number);
                case uint number:
                    return new PropertyValue(number);
                case short number:
                    return new PropertyValue(number);
                case ushort number:
                    return new PropertyValue(number);
                case long number:
                    return new PropertyValue(number);
                case ulong number:
                    return new PropertyValue(number);
                case Asset asset:
                    return new PropertyValue(asset);
                case Archive archive:
                    return new PropertyValue(archive);
                case PropertyValue propertyValue:
                    return propertyValue;
            }

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var valueField = targetType.GetField("Value")?.GetValue(value);
                return await Serialize(valueField);
            }

            if (value is IDictionary dictionary)
            {
                var propertyObject = ImmutableDictionary.CreateBuilder<string, PropertyValue>();
                foreach (var pair in dictionary)
                {
                    if (pair is DictionaryEntry keyValuePair && keyValuePair.Key is string key)
                    {
                        var serializedValue = await Serialize(keyValuePair.Value);
                        propertyObject.Add(key, serializedValue);
                    }
                }

                return new PropertyValue(propertyObject.ToImmutableDictionary());
            }

            if (value is IEnumerable enumerable)
            {
                var elements = ImmutableArray.CreateBuilder<PropertyValue>();

                if (value is IList list && Serializer.InitializedByDefault(list))
                {
                    return new PropertyValue(elements.ToImmutableArray());
                }

                foreach (var element in enumerable)
                {
                    var item = await Serialize(element);
                    elements.Add(item);
                }

                return new PropertyValue(elements.ToImmutableArray());
            }

            if (targetType.IsEnum)
            {
#pragma warning disable CA1305
                var enumValue = Convert.ToInt32(value);
#pragma warning restore CA1305

                return new PropertyValue(enumValue);
            }

            async Task<PropertyValue> SerializeOutput(IOutput output)
            {
                var data = await output.GetDataAsync().ConfigureAwait(false);

                PropertyValue? element = null;
                if (data.IsKnown)
                {
                    element = await Serialize(data.Value);
                }

                var dependantResources = ImmutableHashSet.CreateBuilder<Urn>();
                foreach (var resource in data.Resources)
                {
                    var urn = await resource.Urn.GetValueAsync("").ConfigureAwait(false);
                    dependantResources.Add(new Urn(urn));
                }

                PropertyValue outputValue;
                if (dependantResources.Count == 0)
                {
                    if (element != null)
                    {
                        outputValue = element;
                    }
                    else
                    {
                        outputValue = PropertyValue.Computed;
                    }
                }
                else
                {
                    outputValue = new PropertyValue(new OutputReference(
                        value: element,
                        dependencies: dependantResources.ToImmutable()));
                }

                if (data.IsSecret)
                {
                    return new PropertyValue(outputValue);
                }

                return outputValue;
            }

            if (value is IInput input)
            {
                return await SerializeOutput(input.ToOutput());
            }

            if (value is IOutput output)
            {
                return await SerializeOutput(output);
            }

            if (value is InputArgs inputArgs)
            {
                var inputsMap = await inputArgs.ToDictionaryAsync().ConfigureAwait(false);
                return await Serialize(inputsMap);
            }

            if (targetType.IsClass || targetType.IsValueType)
            {
                var propertyObject = ImmutableDictionary.CreateBuilder<string, PropertyValue>();
                foreach (var property in targetType.GetProperties())
                {
                    var propertyName = PropertyName(property, targetType);
                    var propertyValue = property.GetValue(value);
                    var serializedProperty = await Serialize(propertyValue);
                    propertyObject.Add(propertyName, serializedProperty);
                }

                return new PropertyValue(propertyObject.ToImmutableDictionary());
            }

            return PropertyValue.Null;
        }

        private string DeserializationError(
            PropertyValueType expected,
            PropertyValueType actual,
            Type targetType,
            string[] path)
        {
            if (path.Length == 1 && path[0] == "$")
            {
                return $"Error while deserializing value of type {targetType.Name} from property value of type {actual}. "
                       + $"Expected {expected} instead.";
            }

            var propertyPath = $"[" + string.Join(", ", path) + "]";
            return $"Error while deserializing value of type {targetType.Name} from property value of type {actual}. "
                   + $"Expected {expected} instead at path {propertyPath}.";
        }

        public Task<T> Deserialize<T>(PropertyValue value)
        {
            var deserialized = Deserialize(value, typeof(T));
            if (deserialized is T deserializedValue)
            {
                return Task.FromResult(deserializedValue);
            }

            throw new InvalidOperationException($"Could not deserialize value of type {typeof(T).Name}");
        }

        public object? Deserialize(PropertyValue value, Type targetType)
        {
            var rootPath = new[] { "$" };
            return DeserializeValue(value, targetType, rootPath);
        }

        private object? DeserializeValue(PropertyValue value, Type targetType, string[] path)
        {
            void ThrowTypeMismatchError(PropertyValueType expectedType)
            {
                var error = DeserializationError(
                    expected: expectedType,
                    actual: value.Type,
                    targetType: targetType,
                    path: path);

                throw new InvalidOperationException(error);
            }

            if (targetType == typeof(PropertyValue))
            {
                return value;
            }

            if (IsNullable(targetType))
            {
                if (value.IsNull)
                {
                    return null;
                }

                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    var elementType = targetType.GetGenericArguments()[0];
                    return Activator.CreateInstance(
                        type: targetType,
                        args: new[] { DeserializeValue(value, elementType, path) });
                }
            }

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(InputList<>))
            {
                // change InputList<T> to Output<ImmutableArray<T>> and deserialize
                var elementType = targetType.GetGenericArguments()[0];
                var listType = typeof(ImmutableArray<>).MakeGenericType(elementType);
                var outputListType = typeof(Output<>).MakeGenericType(listType);
                var outputList = DeserializeValue(value, outputListType, path);
                var fromOutputList = targetType.GetMethod(
                    "op_Implicit",
                    types: new[] { outputListType })!;

                return fromOutputList.Invoke(null, new[] { outputList });
            }

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(InputMap<>))
            {
                // change InputMap<T> to Output<ImmutableDictionary<string, T>> and deserialize
                var valueType = targetType.GetGenericArguments()[0];
                var dictionaryType = typeof(ImmutableDictionary<,>).MakeGenericType(typeof(string), valueType);
                var outputMapType = typeof(Output<>).MakeGenericType(dictionaryType);
                var outputDictionary = DeserializeValue(value, outputMapType, path);
                var fromOutputMap = targetType.GetMethod(
                    "op_Implicit",
                    types: new[] { outputMapType })!;

                return fromOutputMap.Invoke(null, new[] { outputDictionary });
            }

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Output<>))
            {
                // deserialize as Input<T> and then convert to Output<T>
                var elementType = targetType.GetGenericArguments()[0];
                var inputType = typeof(Input<>).MakeGenericType(elementType);
                var input = DeserializeValue(value, inputType, path);
                var toOutputMethod =
                    typeof(InputExtensions)
                        .GetMethod("ToOutput", BindingFlags.Public | BindingFlags.Static)!
                        .MakeGenericMethod(elementType);
                return toOutputMethod.Invoke(null, new[] { input });
            }

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Input<>))
            {
                var elementType = targetType.GetGenericArguments()[0];
                var outputType = typeof(Output<>).MakeGenericType(elementType);
                var outputDataType = typeof(OutputData<>).MakeGenericType(elementType);
                var createOutputData = outputDataType.GetConstructor(new[]
                {
                    typeof(ImmutableHashSet<Resource>),
                    elementType,
                    typeof(bool),
                    typeof(bool)
                });

                if (createOutputData == null)
                {
                    throw new InvalidOperationException(
                        $"Could not find constructor for type OutputData<T> with parameters " +
                        $"{nameof(ImmutableHashSet<Resource>)}, {elementType.Name}, bool, bool");
                }

                object CreateOutput(object? outputData)
                {
                    var createOutputMethod =
                        outputType
                            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                            .First(ctor =>
                            {
                                // expected parameter type == Task<OutputData<T>>
                                var parameters = ctor.GetParameters();
                                return parameters.Length == 1 &&
                                    parameters[0].ParameterType == typeof(Task<>).MakeGenericType(outputDataType);
                            })!;

                    var fromResultMethod =
                        typeof(Task)
                            .GetMethod("FromResult")!
                            .MakeGenericMethod(outputDataType)!;

                    return createOutputMethod.Invoke(new[]
                    {
                        fromResultMethod.Invoke(null, new [] { outputData })
                    });
                }

                object CreateInput(object? outputValue)
                {
                    var newInputCtor =
                        typeof(Input<>)
                            .MakeGenericType(elementType!)
                            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                            .First(ctor =>
                            {
                                var parameters = ctor.GetParameters();
                                return parameters.Length == 1
                                       && parameters[0].ParameterType == outputType;
                            });

                    return newInputCtor.Invoke(new[] { outputValue });
                }

                if (value.IsComputed)
                {
                    return CreateInput(CreateOutput(createOutputData.Invoke(new object?[]
                    {
                        ImmutableHashSet<Resource>.Empty, // resources
                        null, // value
                        false, // isKnown
                        false // isSecret
                    })));
                }

                var inputToOutputCast =
                    targetType
                        .GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .First(meth =>
                        {
                            return meth.Name == "op_Implicit" &&
                                meth.ReturnType == outputType;

                        });

                if (value.TryGetSecret(out var secretValue))
                {
                    var inner = DeserializeValue(secretValue, targetType, path);
                    // Combine the inner result (which we _know_ will be an Input<T>) with the secret flag.

                    var createSecret =
                        typeof(Output)
                            .GetMethods(BindingFlags.Public | BindingFlags.Static)
                            .First(meth =>
                            {
                                var parameters = meth.GetParameters();
                                return meth.Name == "CreateSecret" &&
                                    parameters.Length == 1 &&
                                     parameters[0].ParameterType.IsGenericType &&
                                    parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(Output<>);
                            })
                            .MakeGenericMethod(elementType);

                    var secretOutput = createSecret.Invoke(null, new[] { inputToOutputCast.Invoke(null, new[] { inner }) });

                    return CreateInput(secretOutput);
                }

                if (value.TryGetOutput(out var outputValue))
                {
                    var dependencies = ImmutableHashSet.CreateBuilder<Resource>();
                    foreach (var urn in outputValue.Dependencies)
                    {
                        dependencies.Add(new DependencyResource(urn));
                    }
                    var immutableDependencies = dependencies.ToImmutable();

                    if (outputValue.Value == null)
                    {
                        // If the inner value is null this is simply an unknown output, we just need to track in the dependencies from this level.
                        return CreateInput(CreateOutput(createOutputData.Invoke(new object?[]
                        {
                            immutableDependencies, // resources
                            null, // value
                            false, // isKnown
                            false // isSecret
                        })));
                    }

                    // The inner value could be a Secret or a nested OutputReference, deserialise _that_ into
                    // an Input<T> recursively then bind it with the data at this level, adding the extra dependencies.
                    var inner = DeserializeValue(outputValue.Value, targetType, path);
                    var withDependencies = outputType.GetMethod(
                        "WithDependencies", BindingFlags.NonPublic | BindingFlags.Instance)!;

                    var outputWithDependencies = withDependencies.Invoke(
                        inputToOutputCast.Invoke(null, new[] { inner }),
                        new object[] { immutableDependencies });

                    return CreateInput(outputWithDependencies);
                }

                var deserialized = DeserializeValue(value, elementType, path);
                var outputDataValue = createOutputData.Invoke(new object?[]
                {
                    ImmutableHashSet<Resource>.Empty,
                    deserialized,
                    true,
                    false
                });

                return CreateInput(CreateOutput(outputDataValue));
            }

            if (targetType == typeof(int))
            {
                if (value.TryGetNumber(out var numberAsInt))
                {
                    return (int)numberAsInt;
                }

                ThrowTypeMismatchError(PropertyValueType.Number);
            }

            if (targetType == typeof(double))
            {
                if (value.TryGetNumber(out var numberAsDouble))
                {
                    return numberAsDouble;
                }

                ThrowTypeMismatchError(PropertyValueType.Number);
            }

            if (targetType == typeof(float))
            {
                if (value.TryGetNumber(out var numberAsFloat))
                {
                    return (float)numberAsFloat;
                }

                ThrowTypeMismatchError(PropertyValueType.Number);
            }

            if (targetType == typeof(decimal))
            {
                if (value.TryGetNumber(out var numberAsDecimal))
                {
                    return Convert.ToDecimal(numberAsDecimal);
                }

                ThrowTypeMismatchError(PropertyValueType.Number);
            }

            if (targetType == typeof(byte))
            {
                if (value.TryGetNumber(out var numberAsByte))
                {
                    return Convert.ToByte(numberAsByte);
                }

                ThrowTypeMismatchError(PropertyValueType.Number);
            }

            if (targetType == typeof(uint))
            {
                if (value.TryGetNumber(out var numberAsUInt32))
                {
                    return Convert.ToUInt32(numberAsUInt32);
                }

                ThrowTypeMismatchError(PropertyValueType.Number);
            }

            if (targetType == typeof(short))
            {
                if (value.TryGetNumber(out var numberAsShort))
                {
                    return Convert.ToInt16(numberAsShort);
                }

                ThrowTypeMismatchError(PropertyValueType.Number);
            }

            if (targetType == typeof(ushort))
            {
                if (value.TryGetNumber(out var numberAsUnsignedShort))
                {
                    return Convert.ToUInt16(numberAsUnsignedShort);
                }

                ThrowTypeMismatchError(PropertyValueType.Number);
            }

            if (targetType == typeof(long))
            {
                if (value.TryGetNumber(out var numberAsLong))
                {
                    return Convert.ToInt64(numberAsLong);
                }

                ThrowTypeMismatchError(PropertyValueType.Number);
            }

            if (targetType == typeof(ulong))
            {
                if (value.TryGetNumber(out var numberAsLong))
                {
                    return Convert.ToUInt64(numberAsLong);
                }

                ThrowTypeMismatchError(PropertyValueType.Number);
            }

            if (targetType == typeof(string))
            {
                if (value.TryGetString(out var stringValue))
                {
                    return stringValue;
                }

                if (value.IsNull)
                {
                    return null;
                }

                ThrowTypeMismatchError(PropertyValueType.String);
            }

            if (targetType.IsEnum)
            {
                if (value.TryGetNumber(out var enumValue))
                {
                    return Enum.ToObject(targetType, Convert.ToInt32(enumValue));
                }

                ThrowTypeMismatchError(PropertyValueType.Number);
            }

            if (targetType == typeof(Asset))
            {
                if (value.TryGetAsset(out var asset))
                {
                    return asset;
                }

                ThrowTypeMismatchError(PropertyValueType.Asset);
            }

            if (targetType == typeof(Archive))
            {
                if (value.TryGetArchive(out var archive))
                {
                    return archive;
                }

                ThrowTypeMismatchError(PropertyValueType.Archive);
            }

            if (targetType == typeof(bool))
            {
                if (value.TryGetBool(out var boolValue))
                {
                    return boolValue;
                }

                ThrowTypeMismatchError(PropertyValueType.Bool);
            }

            if (targetType.IsArray)
            {
                if (value.TryGetArray(out var arrayValue))
                {
                    var elementType = targetType.GetElementType()!;
                    var array = Array.CreateInstance(elementType, arrayValue.Length);
                    for (var i = 0; i < arrayValue.Length; ++i)
                    {
                        var elementPath = path.Append($"index[{i}]").ToArray();
                        var deserialized = DeserializeValue(arrayValue[i], elementType, elementPath);
                        if (deserialized != null || IsNullable(elementType))
                        {
                            array.SetValue(deserialized, i);
                        }
                    }
                    return array;
                }

                ThrowTypeMismatchError(PropertyValueType.Array);
            }

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (value.TryGetArray(out var arrayValue))
                {
                    var elementType = targetType.GetGenericArguments()[0];
                    var list = Activator.CreateInstance(targetType)!;
                    var addMethod = targetType.GetMethod(nameof(List<int>.Add))!;
                    var index = 0;
                    foreach (var item in arrayValue)
                    {
                        var elementPath = path.Append($"index[{index}]").ToArray();
                        var deserialized = DeserializeValue(item, elementType, elementPath);
                        if (deserialized != null || IsNullable(elementType))
                        {
                            addMethod.Invoke(list, new[] { deserialized });
                        }

                        index++;
                    }
                    return list;
                }

                ThrowTypeMismatchError(PropertyValueType.Array);
            }

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
            {
                var builder =
                    typeof(ImmutableArray).GetMethod(nameof(ImmutableArray.CreateBuilder), Array.Empty<Type>())!
                        .MakeGenericMethod(targetType.GenericTypeArguments)
                        .Invoke(obj: null, parameters: null)!;

                if (value.TryGetArray(out var arrayValue))
                {
                    var builderAdd = builder.GetType().GetMethod(nameof(ImmutableArray<int>.Builder.Add))!;
                    var builderToImmutable = builder.GetType().GetMethod(nameof(ImmutableArray<int>.Builder.ToImmutable))!;
                    var elementType = targetType.GetGenericArguments()[0];
                    var index = 0;
                    foreach (var item in arrayValue)
                    {
                        var elementPath = path.Append($"index[{index}]").ToArray();
                        var deserialized = DeserializeValue(item, elementType, elementPath);
                        if (deserialized != null || IsNullable(elementType))
                        {
                            builderAdd.Invoke(builder, new[] { deserialized });
                        }

                        index++;
                    }

                    return builderToImmutable.Invoke(builder, Array.Empty<object>());
                }

                ThrowTypeMismatchError(PropertyValueType.Array);
            }

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                if (value.TryGetObject(out var values))
                {
                    var dictionary = Activator.CreateInstance(targetType);
                    var addMethod =
                        targetType
                            .GetMethods()
                            .First(methodInfo => methodInfo.Name == "Add" && methodInfo.GetParameters().Length == 2);

                    var valueType = targetType.GenericTypeArguments[1];
                    foreach (var pair in values)
                    {
                        var elementPath = path.Append(pair.Key).ToArray();
                        var deserializedValue = DeserializeValue(pair.Value, valueType, elementPath);
                        if (deserializedValue != null || IsNullable(valueType))
                        {
                            addMethod.Invoke(dictionary, new[] { pair.Key, deserializedValue });
                        }
                    }
                    return dictionary;
                }

                ThrowTypeMismatchError(PropertyValueType.Object);
            }

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(ImmutableDictionary<,>))
            {
                var builder =
                    typeof(ImmutableDictionary).GetMethod(nameof(ImmutableDictionary.CreateBuilder),
                            Array.Empty<Type>())!
                        .MakeGenericMethod(targetType.GenericTypeArguments)
                        .Invoke(obj: null, parameters: null)!;

                var builderAdd =
                    builder
                        .GetType()
                        .GetMethods()
                        .First(methodInfo => methodInfo.Name == "Add" && methodInfo.GetParameters().Length == 2);

                var builderToImmutable = builder.GetType()
                    .GetMethod(nameof(ImmutableDictionary<int, int>.Builder.ToImmutable))!;
                var valueType = targetType.GenericTypeArguments[1];

                if (value.TryGetObject(out var values))
                {
                    foreach (var pair in values)
                    {
                        var elementPath = path.Append(pair.Key).ToArray();
                        var deserializedValue = DeserializeValue(pair.Value, valueType, elementPath);
                        if (deserializedValue != null || IsNullable(valueType))
                        {
                            builderAdd.Invoke(builder, new[] { pair.Key, deserializedValue });
                        }
                    }
                    return builderToImmutable.Invoke(builder, Array.Empty<object>());
                }

                ThrowTypeMismatchError(PropertyValueType.Object);
            }

            if (targetType.IsClass || targetType.IsValueType)
            {
                if (value.TryGetObject(out var objectProperties))
                {
                    return DeserializeObject(objectProperties, targetType, path);
                }
            }

            return null;
        }

        internal static async Task<PropertyValue> From(object? data)
        {
            var serializer = new Serializer(excessiveDebugOutput: false);
            var value = await serializer.SerializeAsync(
                prop: data,
                keepResources: true,
                keepOutputValues: true,
                ctx: "");

            return PropertyValue.Unmarshal(Serializer.CreateValue(value));
        }

        internal async Task<ImmutableDictionary<string, PropertyValue>> StateFromComponentResource(
            ComponentResource component)
        {
            var state = new Dictionary<string, PropertyValue>();
            var componentType = component.GetType();
            var properties = componentType.GetProperties();
            foreach (var property in properties)
            {
                var outputAttribute = property.GetCustomAttribute<OutputAttribute>();

                if (outputAttribute != null)
                {
                    var propertyName = property.Name;
                    if (!string.IsNullOrWhiteSpace(outputAttribute.Name))
                    {
                        propertyName = outputAttribute.Name;
                    }

                    var value = property.GetValue(component);
                    if (value != null)
                    {
                        var serialized = await Serialize(value);
                        state.Add(propertyName, serialized);
                    }
                }
            }

            return state.ToImmutableDictionary();
        }
    }
}

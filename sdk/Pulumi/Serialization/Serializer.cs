// Copyright 2016-2021, Pulumi Corporation

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Enum = System.Enum;

namespace Pulumi.Serialization
{
    internal readonly struct Serializer
    {
        public readonly HashSet<Resource> DependentResources;

        private readonly bool _excessiveDebugOutput;

        public Serializer(bool excessiveDebugOutput)
        {
            DependentResources = new HashSet<Resource>();
            _excessiveDebugOutput = excessiveDebugOutput;
        }

        /// <summary>
        /// Takes in an arbitrary object and serializes it into a uniform form that can converted
        /// trivially to a protobuf to be passed to the Pulumi engine.
        /// <para/>
        /// The allowed 'basis' forms that can be serialized are:
        /// <list type="number">
        /// <item><see langword="null"/>s</item>
        /// <item><see cref="bool"/>s</item>
        /// <item><see cref="int"/>s</item>
        /// <item><see cref="double"/>s</item>
        /// <item><see cref="string"/>s</item>
        /// <item><see cref="Asset"/>s</item>
        /// <item><see cref="Archive"/>s</item>
        /// <item><see cref="Resource"/>s</item>
        /// <item><see cref="ResourceArgs"/></item>
        /// <item><see cref="JsonElement"/></item>
        /// </list>
        /// Additionally, other more complex objects can be serialized as long as they are built
        /// out of serializable objects.  These complex objects include:
        /// <list type="number">
        /// <item><see cref="Input{T}"/>s. As long as they are an Input of a serializable type.</item>
        /// <item><see cref="Output{T}"/>s. As long as they are an Output of a serializable type.</item>
        /// <item><see cref="IList"/>s. As long as all elements in the list are serializable.</item>
        /// <item><see cref="IDictionary"/>. As long as the key of the dictionary are <see cref="string"/>s and as long as the value are all serializable.</item>
        /// </list>
        /// No other forms are allowed.
        /// <para/>
        /// This function will only return values of a very specific shape.  Specifically, the
        /// result values returned will *only* be one of:
        /// <para/>
        /// <list type="number">
        /// <item><see langword="null"/></item>
        /// <item><see cref="bool"/></item>
        /// <item><see cref="int"/></item>
        /// <item><see cref="double"/></item>
        /// <item><see cref="string"/></item>
        /// <item>An <see cref="ImmutableArray{T}"/> containing only these result value types.</item>
        /// <item>An <see cref="IImmutableDictionary{TKey, TValue}"/> where the keys are strings and
        /// the values are only these result value types.</item>
        /// </list>
        /// No other result type are allowed to be returned.
        /// </summary>
        public async Task<object?> SerializeAsync(string ctx, object? prop, bool keepResources, bool keepOutputValues = false,
            bool excludeResourceReferencesFromDependencies = false)
        {
            // IMPORTANT:
            // IMPORTANT: Keep this in sync with serializesPropertiesSync in invoke.ts
            // IMPORTANT:
            if (prop == null ||
                prop is bool ||
                prop is int ||
                prop is double ||
                prop is string)
            {
                if (_excessiveDebugOutput)
                {
                    Log.Debug($"Serialize property[{ctx}]: primitive={prop}");
                }

                return prop;
            }

            if (prop is InputArgs args)
            {
                return await SerializeInputArgsAsync(ctx, args, keepResources, keepOutputValues,
                    excludeResourceReferencesFromDependencies).ConfigureAwait(false);
            }

            if (prop is AssetOrArchive assetOrArchive)
            {
                // There's no need to pass keepOutputValues or excludeResourceReferencesFromDependencies
                // when serializing assets or archives.
                return await SerializeAssetOrArchiveAsync(ctx, assetOrArchive, keepResources).ConfigureAwait(false);
            }

            if (prop is Task)
            {
                throw new InvalidOperationException(
$"Tasks are not allowed inside ResourceArgs. Please wrap your Task in an Output:\n\t{ctx}");
            }

            var propType = prop.GetType();
            // if prop is an InputList<T>
            if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(InputList<>))
            {
                // pull off the Value property from the InputList<T>
                var inputList = propType.GetProperty("Value", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(prop);
                return await SerializeAsync(ctx, inputList, keepResources, keepOutputValues,
                    excludeResourceReferencesFromDependencies).ConfigureAwait(false);
            }
            // if prop is an InputMap<T>
            if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(InputMap<>))
            {
                // pull off the Value property from the InputMap<T>
                var inputList = propType.GetProperty("Value", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(prop);
                return await SerializeAsync(ctx, inputList, keepResources, keepOutputValues,
                    excludeResourceReferencesFromDependencies).ConfigureAwait(false);
            }

            if (prop is IInput input)
            {
                if (_excessiveDebugOutput)
                {
                    Log.Debug($"Serialize property[{ctx}]: Recursing into IInput");
                }

                return await SerializeAsync(ctx, input.ToOutput(), keepResources, keepOutputValues,
                    excludeResourceReferencesFromDependencies).ConfigureAwait(false);
            }

            if (prop is IUnion union)
            {
                if (_excessiveDebugOutput)
                {
                    Log.Debug($"Serialize property[{ctx}]: Recursing into IUnion");
                }

                return await SerializeAsync(ctx, union.Value, keepResources, keepOutputValues,
                    excludeResourceReferencesFromDependencies).ConfigureAwait(false);
            }

            if (prop is JsonElement element)
            {
                if (_excessiveDebugOutput)
                {
                    Log.Debug($"Serialize property[{ctx}]: Recursing into Json");
                }

                return SerializeJson(ctx, element);
            }

            if (prop is IOutput output)
            {
                if (_excessiveDebugOutput)
                {
                    Log.Debug($"Serialize property[{ctx}]: Recursing into Output");
                }
                var data = await output.GetDataAsync().ConfigureAwait(false);
                DependentResources.AddRange(data.Resources);
                var propResources = new HashSet<Resource>(data.Resources);

                // When serializing an Output, we will either serialize it as its resolved value or the "unknown value"
                // sentinel. We will do the former for all outputs created directly by user code (such outputs always
                // resolve isKnown to true) and for any resource outputs that were resolved with known values.
                var isKnown = data.IsKnown;
                var isSecret = data.IsSecret;

                var valueSerializer = new Serializer(_excessiveDebugOutput);

                // It is unsafe to serialize unknown values.
                object? value = isKnown
                    ? await valueSerializer.SerializeAsync(
                        $"{ctx}.id", data.Value, keepResources, keepOutputValues: false,
                        excludeResourceReferencesFromDependencies).ConfigureAwait(false)
                    : null;

                var promiseDeps = valueSerializer.DependentResources;
                DependentResources.UnionWith(promiseDeps);
                propResources.UnionWith(promiseDeps);

                if (keepOutputValues)
                {
                    if (isKnown && !isSecret && propResources.Count == 0)
                    {
                        return value;
                    }

                    var urnDeps = new HashSet<Resource>();
                    foreach (var resource in propResources)
                    {
                        var urnSerializer = new Serializer(_excessiveDebugOutput);
                        await urnSerializer.SerializeAsync($"{ctx} dependency", resource.Urn, keepResources, keepOutputValues: false).ConfigureAwait(false);
                        urnDeps.UnionWith(urnSerializer.DependentResources);
                    }
                    DependentResources.UnionWith(urnDeps);
                    propResources.UnionWith(urnDeps);

                    var dependencies = await Deployment.GetAllTransitivelyReferencedResourceUrnsAsync(propResources).ConfigureAwait(false);
                    var builder = ImmutableDictionary.CreateBuilder<string, object?>();
                    builder.Add(Constants.SpecialSigKey, Constants.SpecialOutputValueSig);
                    if (isKnown)
                    {
                        builder.Add(Constants.ValueName, value);
                    }
                    if (isSecret)
                    {
                        builder.Add(Constants.SecretName, isSecret);
                    }
                    if (dependencies.Count > 0)
                    {
                        builder.Add(Constants.DependenciesName,
                            dependencies.OrderBy(x => x, StringComparer.Ordinal).ToImmutableArray<object>());
                    }
                    return builder.ToImmutable();
                }

                if (!isKnown)
                    return Constants.UnknownValue;

                if (isSecret)
                {
                    var builder = ImmutableDictionary.CreateBuilder<string, object?>();
                    builder.Add(Constants.SpecialSigKey, Constants.SpecialSecretSig);
                    builder.Add(Constants.ValueName, value);
                    return builder.ToImmutable();
                }

                return value;
            }

            if (prop is CustomResource customResource)
            {
                var serializer = this;
                if (keepResources && excludeResourceReferencesFromDependencies)
                {
                    // If we're excluding resource references from dependencies, we don't want to track this
                    // dependency, so we use a new serializer so that when serializing the `id` and `urn`,
                    // the resource won't be included in the caller's `DependentResources`.
                    serializer = new Serializer(_excessiveDebugOutput);
                }
                else
                {
                    DependentResources.Add(customResource);
                }

                var id = await serializer.SerializeAsync($"{ctx}.id", customResource.Id, keepResources, keepOutputValues: false).ConfigureAwait(false);
                if (keepResources)
                {
                    var urn = await serializer.SerializeAsync($"{ctx}.urn", customResource.Urn, keepResources, keepOutputValues: false).ConfigureAwait(false);
                    var builder = ImmutableDictionary.CreateBuilder<string, object?>();
                    builder.Add(Constants.SpecialSigKey, Constants.SpecialResourceSig);
                    builder.Add(Constants.ResourceUrnName, urn);
                    builder.Add(Constants.ResourceIdName, id as string == Constants.UnknownValue ? "" : id);
                    return builder.ToImmutable();
                }
                return id;
            }

            if (prop is ComponentResource componentResource)
            {
                // Component resources often can contain cycles in them.  For example, an awsinfra
                // SecurityGroupRule can point a the awsinfra SecurityGroup, which in turn can point
                // back to its rules through its 'egressRules' and 'ingressRules' properties.  If
                // serializing out the 'SecurityGroup' resource ends up trying to serialize out
                // those properties, a deadlock will happen, due to waiting on the child, which is
                // waiting on the parent.
                //
                // Practically, there is no need to actually serialize out a component.  It doesn't
                // represent a real resource, nor does it have normal properties that need to be
                // tracked for differences (since changes to its properties don't represent changes
                // to resources in the real world).
                //
                // So, to avoid these problems, while allowing a flexible and simple programming
                // model, we just serialize out the component as its urn.  This allows the component
                // to be identified and tracked in a reasonable manner, while not causing us to
                // compute or embed information about it that is not needed, and which can lead to
                // deadlocks.
                if (_excessiveDebugOutput)
                {
                    Log.Debug($"Serialize property[{ctx}]: Encountered ComponentResource");
                }

                var serializer = this;
                if (keepResources && excludeResourceReferencesFromDependencies)
                {
                    // If we're excluding resource references from dependencies, we don't want to track this
                    // dependency, so we use a new serializer so that when serializing the `urn`, the
                    // resource won't be included in the caller's `DependentResources`.
                    serializer = new Serializer(_excessiveDebugOutput);
                }

                var urn = await serializer.SerializeAsync($"{ctx}.urn", componentResource.Urn, keepResources, keepOutputValues: false).ConfigureAwait(false);
                if (keepResources)
                {
                    var builder = ImmutableDictionary.CreateBuilder<string, object?>();
                    builder.Add(Constants.SpecialSigKey, Constants.SpecialResourceSig);
                    builder.Add(Constants.ResourceUrnName, urn);
                    return builder.ToImmutable();
                }
                return urn;
            }

            if (prop is IDictionary dictionary)
            {
                return await SerializeDictionaryAsync(ctx, dictionary, keepResources, keepOutputValues,
                    excludeResourceReferencesFromDependencies).ConfigureAwait(false);
            }

            if (prop is IList list)
            {
                return await SerializeListAsync(ctx, list, keepResources, keepOutputValues,
                    excludeResourceReferencesFromDependencies).ConfigureAwait(false);
            }

            if (prop is Enum e && e.GetTypeCode() == TypeCode.Int32)
            {
                return (int)prop;
            }

            if (prop is ValueTuple)
            {
                return null;
            }

            if (propType.IsValueType && propType.GetCustomAttribute<EnumTypeAttribute>() != null)
            {
                var mi = propType.GetMethod("op_Explicit", BindingFlags.Public | BindingFlags.Static, null, new[] { propType }, null);
                if (mi == null || (mi.ReturnType != typeof(string) && mi.ReturnType != typeof(double)))
                {
                    throw new InvalidOperationException($"Expected {propType.FullName} to have an explicit conversion operator to String or Double.\n\t{ctx}");
                }
                return mi.Invoke(null, new[] { prop });
            }

            if (propType.GetCustomAttribute<OutputTypeAttribute>() != null)
            {
                // Serialize each readonly field, and lowercase the first character of the field name to match
                // the Pulumi convention.
                var result = ImmutableDictionary.CreateBuilder<string, object?>();
                foreach (var field in propType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    if (!field.IsInitOnly)
                    {
                        continue; // Skip non readonly fields.
                    }

                    var fieldName = field.Name;
                    if (fieldName.Length > 0)
                    {
                        fieldName = char.ToLowerInvariant(fieldName[0]) + fieldName.Substring(1);
                    }

                    result[fieldName] = await SerializeAsync(
                        $"{ctx}.{fieldName}",
                        field.GetValue(prop), keepResources, keepOutputValues).ConfigureAwait(false);
                }
                return result.ToImmutable();
            }

            throw new InvalidOperationException($"{propType.FullName} is not a supported argument type.\n\t{ctx}");
        }

        private object? SerializeJson(string ctx, JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Undefined:
                case JsonValueKind.Null:
                    return null;
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    return element.GetDouble();
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetBoolean();
                case JsonValueKind.Array:
                    {
                        var result = ImmutableArray.CreateBuilder<object?>();
                        var index = 0;
                        foreach (var child in element.EnumerateArray())
                        {
                            result.Add(SerializeJson($"{ctx}[{index}]", child));
                            index++;
                        }

                        return result.ToImmutable();
                    }
                case JsonValueKind.Object:
                    {
                        var result = ImmutableDictionary.CreateBuilder<string, object?>();
                        foreach (var x in element.EnumerateObject())
                        {
                            result[x.Name] = SerializeJson($"{ctx}.{x.Name}", x.Value);
                        }

                        return result.ToImmutable();
                    }
                default:
                    throw new InvalidOperationException($"Unknown {nameof(JsonElement)}.{nameof(JsonElement.ValueKind)}: {element.ValueKind}");
            }
        }

        private async Task<ImmutableDictionary<string, object>> SerializeAssetOrArchiveAsync(string ctx, AssetOrArchive assetOrArchive, bool keepResources)
        {
            if (_excessiveDebugOutput)
            {
                Log.Debug($"Serialize property[{ctx}]: asset/archive={assetOrArchive.GetType().Name}");
            }

            if (assetOrArchive is InvalidAsset)
                throw new InvalidOperationException("Cannot serialize invalid asset");
            if (assetOrArchive is InvalidArchive)
                throw new InvalidOperationException("Cannot serialize invalid archive");

            var propName = assetOrArchive.PropName;
            var value = await SerializeAsync(ctx + "." + propName, assetOrArchive.Value, keepResources, keepOutputValues: false,
                excludeResourceReferencesFromDependencies: false).ConfigureAwait(false);

            var builder = ImmutableDictionary.CreateBuilder<string, object>();
            builder.Add(Constants.SpecialSigKey, assetOrArchive.SigKey);
            builder.Add(assetOrArchive.PropName, value!);
            return builder.ToImmutable();
        }

        private async Task<ImmutableDictionary<string, object>> SerializeInputArgsAsync(string ctx, InputArgs args, bool keepResources, bool keepOutputValues,
            bool excludeResourceReferencesFromDependencies)
        {
            if (_excessiveDebugOutput)
            {
                Log.Debug($"Serialize property[{ctx}]: Recursing into ResourceArgs");
            }

            var dictionary = await args.ToDictionaryAsync().ConfigureAwait(false);
            return await SerializeDictionaryAsync(ctx, dictionary, keepResources, keepOutputValues,
                excludeResourceReferencesFromDependencies).ConfigureAwait(false);
        }


        /// <summary>
        /// Returns whether the input list was initialized as default.
        ///
        /// Here, we check whether the generic list is default(ImmutableArray[T])
        /// and return the IsDefaultOrEmpty property value from it using reflection.
        ///
        /// The use of reflection is unavoidable because we cannot _statically_ resolve the
        /// generic type T in ImmutableArray[T].
        /// </summary>
        internal static bool InitializedByDefault(IList list)
        {
            var concreteType = list.GetType();
            if (concreteType.IsGenericType)
            {
                var genericType = concreteType.GetGenericTypeDefinition();
                if (genericType == typeof(ImmutableArray<>))
                {
                    // create a dummy empty instance, int is irrelevant
                    var instance = ImmutableArray.Create<int>();
                    // so that we can get the name of the property using the nameof operator, statically
                    var propertyName = nameof(instance.IsDefaultOrEmpty);
                    var isDefaultOrEmpty = concreteType.GetProperty(propertyName);
                    if (isDefaultOrEmpty != null)
                    {
                        var value = isDefaultOrEmpty.GetValue(list);
                        return value != null && (bool)value;
                    }
                }
            }

            return false;
        }

        private async Task<ImmutableArray<object?>> SerializeListAsync(string ctx, IList list, bool keepResources, bool keepOutputValues,
            bool excludeResourceReferencesFromDependencies)
        {
            var result = await SerializeCollectionAsync(ctx, list, keepResources, keepOutputValues,
                excludeResourceReferencesFromDependencies).ConfigureAwait(false);
            return (ImmutableArray<object?>)result!;
        }

        private async Task<ImmutableDictionary<string, object>> SerializeDictionaryAsync(
            string ctx, IDictionary dictionary, bool keepResources, bool keepOutputValues,
            bool excludeResourceReferencesFromDependencies)
        {
            var result = await SerializeCollectionAsync(ctx, dictionary, keepResources, keepOutputValues,
                excludeResourceReferencesFromDependencies).ConfigureAwait(false);
            return (ImmutableDictionary<string, object>)result!;
        }

        private async Task<object?> SerializeCollectionAsync(
            string ctx, object collection, bool keepResources, bool keepOutputValues,
            bool excludeResourceReferencesFromDependencies)
        {
            // Use an explicit stack for list/dictionary traversal so deeply nested collection values
            // do not consume the CLR call stack.
            var frames = new List<SerializeCollectionFrame>
            {
                new SerializeCollectionFrame(ctx, collection, _excessiveDebugOutput),
            };

            while (frames.Count > 0)
            {
                var frame = frames[^1];
                var child = frame.GetNextChild();
                if (child is not null)
                {
                    if (child.Value is IDictionary || child.Value is IList)
                    {
                        frames.Add(new SerializeCollectionFrame(child.Context, child.Value, _excessiveDebugOutput));
                        continue;
                    }

                    // Non-collection wrappers such as IInput, Output<T>, ResourceArgs, and IUnion still recurse
                    // through SerializeAsync. This loop only protects deeply nested list/dictionary values.
                    frame.AddChild(await SerializeAsync(
                        child.Context, child.Value, keepResources, keepOutputValues,
                        excludeResourceReferencesFromDependencies).ConfigureAwait(false));
                    continue;
                }

                var value = frame.Complete();
                frames.RemoveAt(frames.Count - 1);
                if (frames.Count == 0)
                {
                    return value;
                }

                frames[^1].AddChild(value);
            }

            // The loop either returns the root frame result or pushes/pops a child frame on each iteration.
            throw new InvalidOperationException("Serializer stack was exhausted before producing a result.");
        }

        private sealed class SerializeCollectionFrame
        {
            private readonly string _ctx;
            private readonly bool _excessiveDebugOutput;
            private readonly IList? _list;
            private readonly IDictionary? _dictionary;
            private readonly ImmutableArray<object?>.Builder? _listResult;
            private readonly ImmutableDictionary<string, object>.Builder? _dictionaryResult;
            private readonly IEnumerator? _dictionaryKeys;
            private int _listIndex;
            private string? _currentDictionaryKey;

            public SerializeCollectionFrame(string ctx, object collection, bool excessiveDebugOutput)
            {
                _ctx = ctx;
                _excessiveDebugOutput = excessiveDebugOutput;

                if (collection is IDictionary dictionary)
                {
                    if (_excessiveDebugOutput)
                    {
                        Log.Debug($"Serialize property[{ctx}]: Hit dictionary");
                    }

                    _dictionary = dictionary;
                    _dictionaryKeys = dictionary.Keys.GetEnumerator();
                    _dictionaryResult = ImmutableDictionary.CreateBuilder<string, object>();
                    return;
                }

                if (collection is IList list)
                {
                    if (_excessiveDebugOutput)
                    {
                        Log.Debug($"Serialize property[{ctx}]: Hit list");
                    }

                    _list = list;
                    _listResult = InitializedByDefault(list)
                        ? ImmutableArray.CreateBuilder<object?>()
                        : ImmutableArray.CreateBuilder<object?>(list.Count);
                    return;
                }

                throw new InvalidOperationException(
                    $"{collection.GetType().FullName} is not a supported collection type.\n\t{ctx}");
            }

            public SerializeCollectionChild? GetNextChild()
            {
                if (_list is not null)
                {
                    if (InitializedByDefault(_list) || _listIndex >= _list.Count)
                    {
                        return null;
                    }

                    var index = _listIndex++;
                    if (_excessiveDebugOutput)
                    {
                        Log.Debug($"Serialize property[{_ctx}]: array[{index}] element");
                    }

                    return new SerializeCollectionChild($"{_ctx}[{index}]", _list[index]);
                }

                if (_dictionaryKeys!.MoveNext())
                {
                    if (!(_dictionaryKeys.Current is string stringKey))
                    {
                        throw new InvalidOperationException(
                            $"Dictionaries are only supported with string keys:\n\t{_ctx}");
                    }

                    _currentDictionaryKey = stringKey;
                    if (_excessiveDebugOutput)
                    {
                        Log.Debug($"Serialize property[{_ctx}]: object.{stringKey}");
                    }

                    return new SerializeCollectionChild($"{_ctx}.{stringKey}", _dictionary![stringKey]);
                }

                return null;
            }

            public void AddChild(object? value)
            {
                if (_listResult is not null)
                {
                    _listResult.Add(value);
                    return;
                }

                // When serializing an object, we omit any keys with null values. This matches
                // JSON semantics.
                if (value != null)
                {
                    _dictionaryResult![_currentDictionaryKey!] = value;
                }
            }

            public object Complete()
            {
                if (_listResult is not null)
                {
                    return _listResult.ToImmutable();
                }

                return _dictionaryResult!.ToImmutable();
            }
        }

        private sealed class SerializeCollectionChild
        {
            public SerializeCollectionChild(string context, object? value)
            {
                Context = context;
                Value = value;
            }

            public string Context { get; }

            public object? Value { get; }
        }

        /// <summary>
        /// Internal for testing purposes.
        /// </summary>
        internal static Value CreateValue(object? value)
            => CreateValueIteratively(value);

        private static Value CreateValueIteratively(object? value)
        {
            var frames = new List<CreateValueFrame> { new CreateValueFrame(value) };

            while (frames.Count > 0)
            {
                var frame = frames[^1];
                var child = frame.GetNextChild();
                if (child is not NoChild)
                {
                    frames.Add(new CreateValueFrame(child));
                    continue;
                }

                var result = frame.Complete();
                frames.RemoveAt(frames.Count - 1);
                if (frames.Count == 0)
                {
                    return result;
                }

                frames[^1].AddChild(result);
            }

            // The loop either returns the root frame result or pushes/pops a child frame on each iteration.
            throw new InvalidOperationException("Protobuf value stack was exhausted before producing a result.");
        }

        private sealed class CreateValueFrame
        {
            private readonly object? _value;
            private readonly ImmutableArray<object?>? _list;
            private readonly ImmutableDictionary<string, object?>? _dictionary;
            private readonly List<Value>? _listResult;
            private readonly Struct? _structResult;
            private readonly IEnumerator<string>? _dictionaryKeys;
            private int _listIndex;
            private string? _currentDictionaryKey;

            public CreateValueFrame(object? value)
            {
                _value = value;

                if (value is ImmutableArray<object?> list)
                {
                    _list = list;
                    _listResult = new List<Value>(list.Length);
                    return;
                }

                if (value is ImmutableDictionary<string, object?> dictionary)
                {
                    _dictionary = dictionary;
                    _dictionaryKeys = dictionary.Keys.OrderBy(k => k).GetEnumerator();
                    _structResult = new Struct();
                }
            }

            public object? GetNextChild()
            {
                if (_list.HasValue)
                {
                    if (_listIndex >= _list.Value.Length)
                    {
                        return NoChild.Instance;
                    }

                    return _list.Value[_listIndex++];
                }

                if (_dictionaryKeys is not null)
                {
                    if (!_dictionaryKeys.MoveNext())
                    {
                        return NoChild.Instance;
                    }

                    _currentDictionaryKey = _dictionaryKeys.Current;
                    return _dictionary![_currentDictionaryKey];
                }

                return NoChild.Instance;
            }

            public void AddChild(Value child)
            {
                if (_listResult is not null)
                {
                    _listResult.Add(child);
                    return;
                }

                _structResult!.Fields.Add(_currentDictionaryKey, child);
            }

            public Value Complete()
            {
                if (_listResult is not null)
                {
                    var result = new Value { ListValue = new ListValue() };
                    result.ListValue.Values.AddRange(_listResult);
                    return result;
                }

                if (_structResult is not null)
                {
                    return new Value { StructValue = _structResult };
                }

                return _value switch
                {
                    null => Value.ForNull(),
                    int i => Value.ForNumber(i),
                    double d => Value.ForNumber(d),
                    bool b => Value.ForBool(b),
                    string s => Value.ForString(s),
                    _ => throw new InvalidOperationException(
                        "Unsupported value when converting to protobuf: " + _value.GetType().FullName),
                };
            }
        }

        private sealed class NoChild
        {
            // Null is a valid collection element, so CreateValueFrame uses this sentinel to mean
            // "there is no next child" without conflating that with a null value to serialize.
            public static readonly NoChild Instance = new NoChild();

            private NoChild()
            {
            }
        }

        /// <summary>
        /// Detects encoded `Unknown` values in objects that conform
        /// to the grammar returned by `SerializeAsync`.
        ///
        /// This possibly needs to be revisited to detect `Unknown`
        /// values before `SerializeAsync` converts them, in the more
        /// generic Output representation.
        /// </summary>
        internal static bool ContainsUnknowns(object? value)
            => value switch
            {
                null => false,
                int _ => false,
                double d => false,
                bool b => false,
                string s => s == Constants.UnknownValue,
                ImmutableArray<object> list => list.Any(v => ContainsUnknowns(v)),
                ImmutableDictionary<string, object> dict => dict.AnyValues(v => ContainsUnknowns(v)),
                _ => throw new InvalidOperationException("Unsupported value when converting to protobuf: " + value.GetType().FullName),
            };

        /// <summary>
        /// Given a <see cref="ImmutableDictionary{TKey, TValue}"/> produced by <see cref="SerializeAsync"/>,
        /// produces the equivalent <see cref="Struct"/> that can be passed to the Pulumi engine.
        /// </summary>
        public static Struct CreateStruct(ImmutableDictionary<string, object?> serializedDictionary)
        {
            var result = new Struct();
            foreach (var key in serializedDictionary.Keys.OrderBy(k => k))
            {
                result.Fields.Add(key, CreateValue(serializedDictionary[key]));
            }
            return result;
        }
    }
}

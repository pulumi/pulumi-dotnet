// Copyright 2016-2019, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace Pulumi.Serialization
{
    internal static class Deserializer
    {
        public static OutputData<object?> Deserialize(Value value)
            => DeserializeIteratively(value);

        private static OutputData<object?> DeserializeIteratively(Value value)
        {
            // Use an explicit stack for list/struct traversal so heavily nested protobuf values
            // do not consume the CLR call stack.
            var frames = new List<DeserializeFrame> { new DeserializeFrame(value) };

            while (frames.Count > 0)
            {
                var frame = frames[^1];

                if (!frame.Started)
                {
                    frame.Start();
                }

                if (frame.Result.HasValue)
                {
                    frames.RemoveAt(frames.Count - 1);
                    if (frames.Count == 0)
                    {
                        return frame.Result.Value;
                    }

                    frames[^1].AddChild(frame.Result.Value);
                    continue;
                }

                var child = frame.GetNextChild();
                if (child is not null)
                {
                    frames.Add(new DeserializeFrame(child));
                    continue;
                }

                frame.Complete();
            }

            // The loop either returns the root frame result or pushes/pops a child frame on each iteration.
            throw new InvalidOperationException("Deserializer stack was exhausted before producing a result.");
        }

        private sealed class DeserializeFrame
        {
            private readonly Value _input;
            private Value _value = new Value();
            private bool _wrapperIsSecret;
            private RepeatedField<Value>? _listValues;
            private int _listIndex;
            private ImmutableArray<object?>.Builder? _listResult;
            private IEnumerator<KeyValuePair<string, Value>>? _structEnumerator;
            private ImmutableDictionary<string, object?>.Builder? _structResult;
            private string? _currentStructKey;
            private ImmutableHashSet<Resource>.Builder? _resources;
            private bool _isKnown = true;
            private bool _isSecret;

            public DeserializeFrame(Value input)
            {
                _input = input;
            }

            public bool Started { get; private set; }

            public OutputData<object?>? Result { get; private set; }

            public void Start()
            {
                Started = true;
                var (innerVal, isSecret) = UnwrapSecret(_input);
                _value = innerVal;
                _wrapperIsSecret = isSecret;

                if (_value.KindCase == Value.KindOneofCase.StringValue &&
                    _value.StringValue == Constants.UnknownValue)
                {
                    // Always deserialize unknown as the null value.
                    Result = new OutputData<object?>(
                        ImmutableHashSet<Resource>.Empty, null, isKnown: false, isSecret: _wrapperIsSecret);
                    return;
                }

                if (TryDeserializeAssetOrArchive(_value, out var assetOrArchive))
                {
                    Result = new OutputData<object?>(
                        ImmutableHashSet<Resource>.Empty, assetOrArchive, isKnown: true, isSecret: _wrapperIsSecret);
                    return;
                }
                if (TryDeserializeResource(_value, out var resource))
                {
                    Result = new OutputData<object?>(
                        ImmutableHashSet<Resource>.Empty, resource, isKnown: true, isSecret: _wrapperIsSecret);
                    return;
                }
                if (TryDeserializeOutputValue(_value, out var outputValue))
                {
                    // Note that output values don't really fit-in well with the OutputData<T> model, as they deserialize
                    // to instances of Output<T> which already internally track the resources, known-ness, and secret-ness,
                    // so for the OutputData<T> we just use empty resources, mark it as known, and not a secret.
                    Result = new OutputData<object?>(
                        ImmutableHashSet<Resource>.Empty, outputValue, isKnown: true, isSecret: false);
                    return;
                }

                switch (_value.KindCase)
                {
                    case Value.KindOneofCase.NumberValue:
                        Result = new OutputData<object?>(
                            ImmutableHashSet<Resource>.Empty, _value.NumberValue, isKnown: true, isSecret: _wrapperIsSecret);
                        break;
                    case Value.KindOneofCase.StringValue:
                        Result = new OutputData<object?>(
                            ImmutableHashSet<Resource>.Empty, _value.StringValue, isKnown: true, isSecret: _wrapperIsSecret);
                        break;
                    case Value.KindOneofCase.BoolValue:
                        Result = new OutputData<object?>(
                            ImmutableHashSet<Resource>.Empty, _value.BoolValue, isKnown: true, isSecret: _wrapperIsSecret);
                        break;
                    case Value.KindOneofCase.NullValue:
                        Result = new OutputData<object?>(
                            ImmutableHashSet<Resource>.Empty, null, isKnown: true, isSecret: _wrapperIsSecret);
                        break;
                    case Value.KindOneofCase.ListValue:
                        _listValues = _value.ListValue.Values;
                        _listResult = ImmutableArray.CreateBuilder<object?>();
                        _resources = ImmutableHashSet.CreateBuilder<Resource>();
                        break;
                    case Value.KindOneofCase.StructValue:
                        _structEnumerator = _value.StructValue.Fields.GetEnumerator();
                        _structResult = ImmutableDictionary.CreateBuilder<string, object?>();
                        _resources = ImmutableHashSet.CreateBuilder<Resource>();
                        break;
                    case Value.KindOneofCase.None:
                        throw new InvalidOperationException("Should never get 'None' type when deserializing protobuf");
                    default:
                        throw new InvalidOperationException("Unknown type when deserializing protobuf: " + _value.KindCase);
                }
            }

            public Value? GetNextChild()
            {
                if (_listValues is not null)
                {
                    if (_listIndex >= _listValues.Count)
                    {
                        return null;
                    }

                    return _listValues[_listIndex++];
                }

                if (_structEnumerator is not null)
                {
                    if (!_structEnumerator.MoveNext())
                    {
                        return null;
                    }

                    var current = _structEnumerator.Current;
                    _currentStructKey = current.Key;
                    return current.Value;
                }

                return null;
            }

            public void AddChild(OutputData<object?> child)
            {
                (_isKnown, _isSecret) = OutputData.Combine(child, _isKnown, _isSecret);
                _resources!.UnionWith(child.Resources);

                if (_listResult is not null)
                {
                    _listResult.Add(child.Value);
                    return;
                }

                _structResult!.Add(_currentStructKey!, child.Value);
            }

            public void Complete()
            {
                if (_listResult is not null)
                {
                    Result = OutputData.Create(
                        _resources!.ToImmutable(), _listResult.ToImmutable(), _isKnown, _wrapperIsSecret || _isSecret);
                    return;
                }

                if (_structResult is not null)
                {
                    Result = OutputData.Create(
                        _resources!.ToImmutable(), _structResult.ToImmutable(), _isKnown, _wrapperIsSecret || _isSecret);
                    return;
                }

                throw new InvalidOperationException("Cannot complete a primitive deserializer frame.");
            }
        }

        private static (Value unwrapped, bool isSecret) UnwrapSecret(Value value)
        {
            var isSecret = false;

            while (IsSpecialStruct(value, out var sig) &&
                   sig == Constants.SpecialSecretSig)
            {
                if (!value.StructValue.Fields.TryGetValue(Constants.ValueName, out var secretValue))
                    throw new InvalidOperationException("Secrets must have a field called 'value'");

                isSecret = true;
                value = secretValue;
            }

            return (value, isSecret);
        }

        private static bool IsSpecialStruct(
            Value value, [NotNullWhen(true)] out string? sig)
        {
            if (value.KindCase == Value.KindOneofCase.StructValue &&
                value.StructValue.Fields.TryGetValue(Constants.SpecialSigKey, out var sigVal) &&
                sigVal.KindCase == Value.KindOneofCase.StringValue)
            {
                sig = sigVal.StringValue;
                return true;
            }

            sig = null;
            return false;
        }

        private static bool TryDeserializeAssetOrArchive(
            Value value, [NotNullWhen(true)] out AssetOrArchive? assetOrArchive)
        {
            if (IsSpecialStruct(value, out var sig))
            {
                if (sig == Constants.SpecialAssetSig)
                {
                    assetOrArchive = DeserializeAsset(value);
                    return true;
                }
                if (sig == Constants.SpecialArchiveSig)
                {
                    assetOrArchive = DeserializeArchive(value);
                    return true;
                }
            }

            assetOrArchive = null;
            return false;
        }

        private static Archive DeserializeArchive(Value value)
        {
            if (TryGetStringValue(value.StructValue.Fields, Constants.AssetOrArchivePathName, out var path))
                return new FileArchive(path);

            if (TryGetStringValue(value.StructValue.Fields, Constants.AssetOrArchiveUriName, out var uri))
                return new RemoteArchive(uri);

            if (value.StructValue.Fields.TryGetValue(Constants.ArchiveAssetsName, out var assetsValue))
            {
                if (assetsValue.KindCase == Value.KindOneofCase.StructValue)
                {
                    var assets = ImmutableDictionary.CreateBuilder<string, AssetOrArchive>();
                    foreach (var (name, val) in assetsValue.StructValue.Fields)
                    {
                        if (!TryDeserializeAssetOrArchive(val, out var innerAssetOrArchive))
                            throw new InvalidOperationException("AssetArchive contained an element that wasn't itself an Asset or Archive.");

                        assets[name] = innerAssetOrArchive;
                    }

                    return new AssetArchive(assets.ToImmutable());
                }
            }

            throw new InvalidOperationException("Value was marked as Archive, but did not conform to required shape.");
        }

        private static Asset DeserializeAsset(Value value)
        {
            if (TryGetStringValue(value.StructValue.Fields, Constants.AssetOrArchivePathName, out var path))
                return new FileAsset(path);

            if (TryGetStringValue(value.StructValue.Fields, Constants.AssetOrArchiveUriName, out var uri))
                return new RemoteAsset(uri);

            if (TryGetStringValue(value.StructValue.Fields, Constants.AssetTextName, out var text))
                return new StringAsset(text);

            throw new InvalidOperationException("Value was marked as Asset, but did not conform to required shape.");
        }

        private static bool TryDeserializeResource(
            Value value, [NotNullWhen(true)] out Resource? resource)
        {
            if (!IsSpecialStruct(value, out var sig) || sig != Constants.SpecialResourceSig)
            {
                resource = null;
                return false;
            }

            if (!TryGetStringValue(value.StructValue.Fields, Constants.ResourceUrnName, out var urn))
            {
                throw new InvalidOperationException("Value was marked as a Resource, but did not conform to required shape.");
            }

            if (!TryGetStringValue(value.StructValue.Fields, Constants.ResourceVersionName, out var version))
            {
                version = "";
            }

            var urnParts = urn.Split("::");
            var qualifiedType = urnParts[2];
            var qualifiedTypeParts = qualifiedType.Split('$');
            var type = qualifiedTypeParts[^1];

            if (ResourcePackages.TryConstruct(type, version, urn, out resource))
            {
                return true;
            }

            resource = new DependencyResource(urn);
            return true;
        }

        private static bool TryDeserializeOutputValue(
            Value value, [NotNullWhen(true)] out object? result)
        {
            if (!IsSpecialStruct(value, out var sig) || sig != Constants.SpecialOutputValueSig)
            {
                result = null;
                return false;
            }

            bool isKnown = value.StructValue.Fields.TryGetValue(Constants.ValueName, out var val);
            bool isSecret = value.StructValue.Fields.TryGetValue(Constants.SecretName, out var secret) &&
                secret.KindCase == Value.KindOneofCase.BoolValue && secret.BoolValue;

            var dependencies = ImmutableHashSet<Resource>.Empty;
            if (value.StructValue.Fields.TryGetValue(Constants.DependenciesName, out var deps) &&
                deps.KindCase == Value.KindOneofCase.ListValue)
            {
                var resources = ImmutableHashSet.CreateBuilder<Resource>();
                foreach (var dep in deps.ListValue.Values)
                {
                    if (dep.KindCase == Value.KindOneofCase.StringValue)
                    {
                        resources.Add(new DependencyResource(dep.StringValue));
                    }
                }
                dependencies = resources.ToImmutable();
            }

            var resultValue = isKnown ? Deserialize(val).Value : null;

            result = CreateOutput(dependencies, resultValue, isKnown, isSecret);
            return true;
        }

        private static object CreateOutput(
            ImmutableHashSet<Resource> resources, object? value, bool isKnown, bool isSecret)
        {
            var elementType = typeof(object);
            if (value is not null)
            {
                elementType = value.GetType();
            }
            var outputDataType = typeof(OutputData<>).MakeGenericType(elementType);
            var createOutputData = outputDataType.GetConstructor(new[]
            {
                typeof(ImmutableHashSet<Resource>),
                elementType,
                typeof(bool),
                typeof(bool)
            });
            if (createOutputData is null)
            {
                throw new InvalidOperationException(
                    $"Could not find constructor for type OutputData<T> with parameters " +
                    $"{nameof(ImmutableHashSet<Resource>)}, {elementType.Name}, bool, bool");
            }

            var typedOutputData = createOutputData.Invoke(new object?[] {
                resources,
                value,
                isKnown,
                isSecret });

            var createOutputMethod =
                typeof(Output<>)
                    .MakeGenericType(elementType)
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

            return createOutputMethod.Invoke(new[] { fromResultMethod.Invoke(null, new[] { typedOutputData }) });
        }

        private static bool TryGetStringValue(
            MapField<string, Value> fields, string keyName, [NotNullWhen(true)] out string? result)
        {
            if (fields.TryGetValue(keyName, out var value) &&
                value.KindCase == Value.KindOneofCase.StringValue)
            {
                result = value.StringValue;
                return true;
            }

            result = null;
            return false;
        }
    }
}

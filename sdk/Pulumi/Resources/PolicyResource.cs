// Copyright 2016-2025, Pulumi Corporation

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Pulumi.Serialization;

namespace Pulumi
{
    public class ResourceIdentity
    {
        public string Type { get; }
        public string Version { get; }
        public string Urn { get; }

        public ResourceIdentity(string type, string version, string urn)
        {
            this.Type = type;
            this.Version = version;
            this.Urn = urn;
        }
    }

    /// <summary>
    /// PolicyResource represents the base class for all policy packs objects.
    /// </summary>
    public abstract class PolicyResource
    {
        internal class ValueAndFlag
        {
            internal readonly FieldInfo Value;
            internal readonly FieldInfo Flag;

            internal ValueAndFlag(FieldInfo value, FieldInfo flag)
            {
                this.Value = value;
                this.Flag = flag;
            }
        }

        internal class DeserializedValue
        {
            internal static readonly DeserializedValue Empty = new DeserializedValue();

            internal readonly object? Value;
            internal readonly bool Unknown;

            internal DeserializedValue()
            {
                Value = null;
                Unknown = true;
            }

            internal DeserializedValue(object? value)
            {
                Value = value;
                Unknown = false;
            }
        }

        private static readonly ConcurrentDictionary<System.Type, Dictionary<String, ValueAndFlag>> _sLookupFields = new();

        private static T CreateInstance<T>(System.Type t, params object?[]? args)
        {
            return (T)(Activator.CreateInstance(t, args) ?? throw new ArgumentException($"Cannot create instance of type {t.Name}."));
        }

        private static Dictionary<String, ValueAndFlag> GetFieldsMapping(System.Type t)
        {
            if (!_sLookupFields.TryGetValue(t, out var res))
            {
                res = new Dictionary<string, ValueAndFlag>();

                var fieldLookup = new Dictionary<String, FieldInfo>();

                foreach (var field in t.GetFields(BindingFlags.Default | BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    fieldLookup[field.Name] = field;
                }

                foreach (var field in fieldLookup.Values)
                {
                    foreach (var attr in field.GetCustomAttributes())
                    {
                        if (attr is PolicyResourcePropertyAttribute attr2)
                        {
                            var entry = new ValueAndFlag(field, fieldLookup[attr2.Flag]);
                            res.Add(attr2.Name, entry);
                        }
                    }
                }

                _sLookupFields[t] = res;
            }

            return res;
        }

        public static object Deserialize(Struct args, System.Type type, bool asInput)
        {
            object result = CreateInstance<object>(type);

            var map = GetFieldsMapping(type);

            foreach (var entry in args.Fields)
            {
                if (!map.TryGetValue(entry.Key, out var pair))
                {
                    // Ignore missing fields
                    continue;
                }

                // var fieldFieldType = field.FieldType;

                var valueData = DeserializeInner(entry.Value, pair.Value.FieldType, asInput);
                pair.Value.SetValue(result, valueData.Value);
                pair.Flag.SetValue(result, valueData.Unknown);
            }

            return result;
        }

        private static DeserializedValue DeserializeInner(Value value, System.Type type, bool asInput)
        {
            value = UnwrapSecret(value);

            if (value.KindCase == Value.KindOneofCase.StringValue)
            {
                if (value.StringValue == Constants.UnknownValue)
                {
                    // Always deserialize unknown as the empty value.
                    return DeserializedValue.Empty;
                }

                if (type != typeof(string))
                {
                    value = (Value)JsonParser.Default.Parse(value.StringValue, Value.Descriptor);
                }
            }

            if (TryDeserializeAssetOrArchive(value, out var assetOrArchive))
            {
                return new DeserializedValue(assetOrArchive);
            }

            if (TryDeserializeResource(value, asInput, out var resource))
            {
                return new DeserializedValue(resource);
            }

            switch (value.KindCase)
            {
                case Value.KindOneofCase.NumberValue:
                    return new DeserializedValue(value.NumberValue);

                case Value.KindOneofCase.StringValue:
                    return new DeserializedValue(value.StringValue);

                case Value.KindOneofCase.BoolValue:
                    return new DeserializedValue(value.BoolValue);

                case Value.KindOneofCase.StructValue:
                {
                    var structValue = value.StructValue;

                    if (typeof(IDictionary).IsAssignableFrom(type))
                    {
                        var subType = type.GenericTypeArguments[1] ?? throw new InvalidOperationException();

                        var result = CreateInstance<IDictionary>(type);

                        foreach (var entry in structValue.Fields)
                        {
                            var key = entry.Key;
                            var element = entry.Value;

                            // Unilaterally skip properties considered internal by the Pulumi engine.
                            // These don't actually contribute to the exposed shape of the object, do
                            // not need to be passed back to the engine, and often will not match the
                            // expected type we are deserializing into.
                            if (key.StartsWith("__", StringComparison.Ordinal))
                            {
                                continue;
                            }

                            var elementData = DeserializeInner(element, subType, asInput);
                            if (elementData.Unknown || elementData.Value == null)
                            {
                                continue; // skip null early, because most collections cannot handle null values
                            }

                            result[key] = elementData.Value;
                        }

                        return new DeserializedValue(result);
                    }
                    else
                    {
                        var obj = Deserialize(structValue, type, asInput);
                        return new DeserializedValue(obj);
                    }
                }

                case Value.KindOneofCase.ListValue:
                {
                    var subType = type.GenericTypeArguments[0] ?? throw new InvalidOperationException();

                    var result = CreateInstance<IList>(type);

                    foreach (var element in value.ListValue.Values)
                    {
                        var elementData = DeserializeInner(element, subType, asInput);
                        if (elementData.Unknown)
                        {
                            // If any of the values in the list are unknown, the whole list is treated as unknown.
                            return DeserializedValue.Empty;
                        }

                        result.Add(elementData.Value);
                    }

                    return new DeserializedValue(result);
                }

                case Value.KindOneofCase.NullValue:
                    return new DeserializedValue(null);

                case Value.KindOneofCase.None:
                    throw new NotSupportedException("Should never get 'None' type when deserializing protobuf");
                default:
                    throw new NotSupportedException($"Unknown type when deserializing protobuf: {value.KindCase}");
            }
        }

        private static Value UnwrapSecret(Value value)
        {
            if (CheckSpecialStruct(value, out var sig) && Constants.SpecialSecretSig == sig)
            {
                if (!TryGetValue(value.StructValue, Constants.SecretName, out var secretValue))
                {
                    throw new NotSupportedException("Secrets must have a field called 'value'");
                }

                return UnwrapSecret(secretValue);
            }

            return value;
        }

        /**
         * @return signature of the special Struct or empty if not a special Struct
         */
        private static bool CheckSpecialStruct(Value value, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? result)
        {
            if (value.KindCase == Value.KindOneofCase.StructValue)
            {
                foreach (var entry in value.StructValue.Fields)
                {
                    if (entry.Key == Constants.SpecialSigKey)
                    {
                        if (entry.Value.KindCase == Value.KindOneofCase.StringValue)
                        {
                            result = entry.Value.StringValue;
                            return true;
                        }
                    }
                }
            }

            result = null;
            return false;
        }

        private static bool TryDeserializeAssetOrArchive(Value value, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AssetOrArchive? assetOrArchive)
        {
            if (CheckSpecialStruct(value, out var sig))
            {
                if (Constants.SpecialAssetSig == sig)
                {
                    assetOrArchive = DeserializeAsset(value);
                    return true;
                }

                if (Constants.SpecialArchiveSig == sig)
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
            if (value.KindCase != Value.KindOneofCase.StructValue)
            {
                throw new ArgumentException($"Expected Value kind of Struct, got: {value.KindCase}");
            }

            if (TryGetStringValue(value.StructValue, Constants.AssetOrArchivePathName, out var path))
            {
                return new FileArchive(path);
            }

            if (TryGetStringValue(value.StructValue, Constants.AssetOrArchiveUriName, out var uri))
            {
                return new RemoteArchive(uri);
            }

            if (TryGetStructValue(value.StructValue, Constants.ArchiveAssetsName, out var assets))
            {
                var result = new Dictionary<string, AssetOrArchive>();

                foreach (var pair in assets.Fields)
                {
                    if (!TryDeserializeAssetOrArchive(pair.Value, out var assetOrArchive))
                    {
                        throw new NotSupportedException("AssetArchive contained an element that wasn't itself an Asset or Archive.");
                    }

                    result[pair.Key] = assetOrArchive;
                }

                return new AssetArchive(result);
            }

            throw new NotSupportedException("Value was marked as Archive, but did not conform to required shape.");
        }

        private static Asset DeserializeAsset(Value value)
        {
            if (value.KindCase != Value.KindOneofCase.StructValue)
            {
                throw new ArgumentException($"Expected Value kind of Struct, got: {value.KindCase}");
            }

            if (TryGetStringValue(value.StructValue, Constants.AssetOrArchivePathName, out var path))
            {
                return new FileAsset(path);
            }

            if (TryGetStringValue(value.StructValue, Constants.AssetOrArchiveUriName, out var uri))
            {
                return new RemoteAsset(uri);
            }

            if (TryGetStringValue(value.StructValue, Constants.AssetTextName, out var text))
            {
                return new StringAsset(text);
            }

            throw new NotSupportedException("Value was marked as Asset, but did not conform to required shape.");
        }

        private static bool TryDeserializeResource(Value value, bool asInput, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PolicyResource? result)
        {
            var id = TryDecodingResourceIdentity(value);
            if (id == null)
            {
                result = null;
                return false;
            }

            var resourceClass = asInput
                ? PolicyResourcePackages.ResolveInputType(id.Type, id.Version)
                : PolicyResourcePackages.ResolveOutputType(id.Type, id.Version);
            if (resourceClass == null)
            {
                throw new NotSupportedException("Value was marked as a Resource, but did not map to any known resource type.");
            }

            result = (PolicyResource)Deserialize(value.StructValue, resourceClass, asInput);
            return true;
        }

        static ResourceIdentity? TryDecodingResourceIdentity(Value value)
        {
            if (!CheckSpecialStruct(value, out var sig) || Constants.SpecialResourceSig != sig)
            {
                return null;
            }

            var structValue = value.StructValue;

            if (!TryGetStringValue(structValue, Constants.ResourceUrnName, out var urn))
            {
                throw new NotSupportedException("Value was marked as a Resource, but did not conform to required shape.");
            }

            if (!TryGetStringValue(structValue, Constants.ResourceVersionName, out var version))
            {
                version = "";
            }

            var urnParsed = Urn.Parse(urn);
            var type = urnParsed.QualifiedType;

            return new ResourceIdentity(type, version, urn);
        }

        static bool TryGetStringValue(Struct fields,
            string key,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
            out string? result)
        {
            if (TryGetValue(fields, key, out var value) && value.KindCase == Value.KindOneofCase.StringValue)
            {
                result = value.StringValue;
                return true;
            }

            result = null;
            return false;
        }

        static bool TryGetStructValue(Struct fields,
            string key,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
            out Struct? result)
        {
            if (TryGetValue(fields, key, out var value) && value.KindCase == Value.KindOneofCase.StructValue)
            {
                result = value.StructValue;
                return true;
            }

            result = null;
            return false;
        }

        static bool TryGetValue(Struct fields, string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Value? result)
        {
            return fields.Fields.TryGetValue(key, out result);
        }
    }

    /// <summary>
    /// PolicyResourceInput represents the common class for all Policy Pack Input states.
    /// </summary>
    public abstract class PolicyResourceInput : PolicyResource
    {
    }

    /// <summary>
    /// PolicyResourceInput represents the common class for all Policy Pack Output states.
    /// </summary>
    public abstract class PolicyResourceOutput : PolicyResource
    {
    }
}

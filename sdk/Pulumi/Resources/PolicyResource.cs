// Copyright 2016-2019, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Pulumi.Serialization;

namespace Pulumi
{
    public class ResourceIdentity
    {
        public readonly string Type;
        public readonly string Version;
        public readonly string Urn;

        public ResourceIdentity(string type, string version, string urn)
        {
            this.Type = type;
            this.Version = version;
            this.Urn = urn;
        }
    }

    /// <summary>
    /// PolicyResource represents a class whose CRUD operations are implemented by a provider plugin.
    /// </summary>
    public abstract class PolicyResource
    {
        public static object Deserialize(Struct args, System.Type type)
        {
            object? result = null;

            foreach (var constructor in type.GetConstructors())
            {
                var parameters = constructor.GetParameters();
                if (parameters.Length == 0)
                {
                    result = constructor.Invoke(null);
                    break;
                }
            }

            if (result == null)
            {
                throw new Exception($"Cannot create instance of type {type.Name}.");
            }

            var map = new Dictionary<string, FieldInfo>();

            foreach (var field in type.GetFields())
            {
                foreach (var attr in field.GetCustomAttributes())
                {
                    if (attr is InputAttribute attr2)
                    {
                        map.Add(attr2.Name, field);
                    }
                }
            }

            foreach (var entry in args.Fields)
            {
                if (!map.TryGetValue(entry.Key, out var field))
                {
                    // Ignore missing fields
                    continue;
                }

                var fieldFieldType = field.FieldType;
                var valueData = DeserializeInner(entry.Value, fieldFieldType);
                if (valueData is String valueAsStr && fieldFieldType != typeof(string))
                {
                    var value = (Value)JsonParser.Default.Parse(valueAsStr, Value.Descriptor);
                    valueData = DeserializeInner(value, fieldFieldType);
                }

                if (valueData != null)
                {
                    field.SetValue(result, valueData);
                }
            }

            return result;
        }

        public static object? DeserializeInner(Value value, System.Type type)
        {
            return DeserializeCore(value, v =>
            {
                switch (v.KindCase)
                {
                    case Value.KindOneofCase.NumberValue:
                        return DeserializeDouble(v);

                    case Value.KindOneofCase.StringValue:
                        return DeserializeString(v);

                    case Value.KindOneofCase.BoolValue:
                        return DeserializeBoolean(v);

                    case Value.KindOneofCase.StructValue:
                        return DeserializeStruct(v, type.GetElementType() ?? throw new InvalidOperationException());

                    case Value.KindOneofCase.ListValue:
                        return DeserializeList(v, type.GetElementType() ?? throw new InvalidOperationException());

                    case Value.KindOneofCase.NullValue:
                        return null;

                    case Value.KindOneofCase.None:
                        throw new NotSupportedException("Should never get 'None' type when deserializing protobuf");
                    default:
                        throw new NotSupportedException($"Unknown type when deserializing protobuf: {v.KindCase}");
                }
            });
        }

        private static object? DeserializeCore(Value maybeSecret, Func<Value, object?> func)
        {
            var value = UnwrapSecret(maybeSecret);

            if (value is { KindCase: Value.KindOneofCase.StringValue, StringValue: Constants.UnknownValue })
            {
                // always deserialize unknown as the null value.
                return null;
            }

            if (TryDeserializeAssetOrArchive(value, out var assetOrArchive))
            {
                return assetOrArchive;
            }

            if (TryDeserializeResource(value, out var resource))
            {
                return resource;
            }

            return func.Invoke(value);
        }

        private static bool DeserializeBoolean(Value value)
        {
            return (bool)(deserializeOneOf(value, Value.KindOneofCase.BoolValue, v => value.BoolValue) ?? throw new InvalidOperationException());
        }

        private static string DeserializeString(Value value)
        {
            return (string)(deserializeOneOf(value, Value.KindOneofCase.StringValue, v => value.StringValue) ?? throw new InvalidOperationException());
        }

        private static double DeserializeDouble(Value value)
        {
            return (double)(deserializeOneOf(value, Value.KindOneofCase.NumberValue, v => value.NumberValue) ?? throw new InvalidOperationException());
        }

        private static object? DeserializeList(Value value, System.Type type)
        {
            return deserializeOneOf(value, Value.KindOneofCase.ListValue, v =>
            {
                var result = new List<object?>(); // will hold nulls

                foreach (var element in v.ListValue.Values)
                {
                    result.Add(DeserializeInner(element, type));
                }

                return result;
            });
        }

        private static object? DeserializeStruct(Value value, System.Type type)
        {
            return deserializeOneOf(value, Value.KindOneofCase.StructValue, v =>
            {
                var result = new Dictionary<string, object>();

                foreach (var entry in v.StructValue.Fields)
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

                    var elementData = DeserializeInner(element, type);
                    if (elementData == null)
                    {
                        continue; // skip null early, because most collections cannot handle null values
                    }

                    result[key] = elementData;
                }

                return result.ToImmutableDictionary();
            });
        }

        private static object? deserializeOneOf(Value value, Value.KindOneofCase kind, Func<Value, object?> func)
        {
            return DeserializeCore(value, v =>
            {
                if (v.KindCase != kind)
                {
                    throw new NotSupportedException($"Trying to deserialize '{v.KindCase}' as a '{kind}'");
                }

                return func.Invoke(v);
            });
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

        private static bool TryDeserializeResource(Value value, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PolicyResource? result)
        {
            var id = TryDecodingResourceIdentity(value);
            if (id == null)
            {
                result = null;
                return false;
            }

            var resourceClass = PolicyResourcePackages.ResolveType(id.Type, id.Version);
            if (resourceClass == null)
            {
                throw new NotSupportedException("Value was marked as a Resource, but did not map to any known resource type.");
            }

            result = (PolicyResource)Deserialize(value.StructValue, resourceClass);
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
}

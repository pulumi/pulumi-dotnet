using Pulumi.Serialization;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Pulumi.Experimental
{
    /// <summary>
    /// Property values will be one of these types.
    /// </summary>
    public enum PropertyValueType
    {
        Null,
        Bool,
        Number,
#pragma warning disable CA1720 // Identifier contains type name
        String,
        Array,
        Map,
#pragma warning restore CA1720 // Identifier contains type name
        Asset,
        Archive,
        Resource,
        Computed,
    }

    public readonly struct ResourceReference : IEquatable<ResourceReference>
    {
        public Urn URN { get; }
        public PropertyValue? Id { get; }
        public string PackageVersion { get; }

        public ResourceReference(Urn urn, PropertyValue? id, string version)
        {
            URN = urn;
            Id = id;
            PackageVersion = version;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(URN, Id, PackageVersion);
        }

        public override bool Equals(object? obj)
        {
            if (obj is ResourceReference reference)
            {
                return Equals(reference);
            }
            return false;
        }

        public bool Equals(ResourceReference other)
        {
            return URN == other.URN && Equals(Id, other.Id) && PackageVersion == other.PackageVersion;
        }

        public static bool operator ==(ResourceReference left, ResourceReference right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ResourceReference left, ResourceReference right)
        {
            return !(left == right);
        }
    }

    public sealed class PropertyValue : IEquatable<PropertyValue>
    {
        public PropertyValueType Type
        {
            get
            {
                if (BoolValue != null)
                {
                    return PropertyValueType.Bool;
                }
                else if (NumberValue != null)
                {
                    return PropertyValueType.Number;
                }
                else if (StringValue != null)
                {
                    return PropertyValueType.String;
                }
                else if (ArrayValue != null)
                {
                    return PropertyValueType.Array;
                }
                else if (MapValue != null)
                {
                    return PropertyValueType.Map;
                }
                else if (AssetValue != null)
                {
                    return PropertyValueType.Asset;
                }
                else if (ArchiveValue != null)
                {
                    return PropertyValueType.Archive;
                }
                else if (ResourceValue != null)
                {
                    return PropertyValueType.Resource;
                }
                else if (IsComputed)
                {
                    return PropertyValueType.Computed;
                }
                else
                {
                    return PropertyValueType.Null;
                }
            }
        }

        public bool IsNull
        {
            get
            {
                // Null if all the other properties aren't set
                return
                    BoolValue == null &&
                    NumberValue == null &&
                    StringValue == null &&
                    ArrayValue == null &&
                    MapValue == null &&
                    AssetValue == null &&
                    ArchiveValue == null &&
                    ResourceValue == null &&
                    !IsComputed;
            }
        }

        public bool IsComputed => _isComputed;

        private readonly bool? BoolValue;
        private readonly double? NumberValue;
        private readonly string? StringValue;
        private readonly ImmutableArray<PropertyValue>? ArrayValue;
        private readonly ImmutableDictionary<string, PropertyValue>? MapValue;
        private readonly Asset? AssetValue;
        private readonly Archive? ArchiveValue;
        private readonly ResourceReference? ResourceValue;
        private readonly bool _isComputed;

        public bool IsSecret { get; init; }

        public ImmutableHashSet<Urn> Dependencies { get; init; }

        public bool Equals(PropertyValue? other)
        {
            if (other == null)
            {
                return false;
            }

            if (IsSecret != other.IsSecret)
            {
                return false;
            }
            if (!Dependencies.SetEquals(other.Dependencies))
            {
                return false;
            }

            if (BoolValue.HasValue && other.BoolValue.HasValue)
            {
                return BoolValue.Value == other.BoolValue.Value;
            }
            else if (NumberValue.HasValue && other.NumberValue.HasValue)
            {
                return NumberValue.Value == other.NumberValue.Value;
            }
            else if (StringValue != null && other.StringValue != null)
            {
                return StringValue == other.StringValue;
            }
            else if (ArrayValue.HasValue && other.ArrayValue.HasValue)
            {
                var self = ArrayValue.Value;
                var them = other.ArrayValue.Value;
                if (self.Length != them.Length)
                {
                    return false;
                }
                for (var i = 0; i < self.Length; ++i)
                {
                    if (!self[i].Equals(them[i]))
                    {
                        return false;
                    }
                }
                return true;
            }
            else if (MapValue != null && other.MapValue != null)
            {
                var self = MapValue;
                var them = other.MapValue;
                if (self.Count != them.Count)
                {
                    return false;
                }
                foreach (var kv in self)
                {
                    if (!them.TryGetValue(kv.Key, out var theirs))
                    {
                        return false;
                    }
                    if (!kv.Value.Equals(theirs))
                    {
                        return false;
                    }
                }
                return true;
            }
            else if (AssetValue != null && other.AssetValue != null)
            {
                return AssetValue.Equals(other.AssetValue);
            }
            else if (ArchiveValue != null && other.ArchiveValue != null)
            {
                return ArchiveValue.Equals(other.ArchiveValue);
            }
            else if (ResourceValue != null && other.ResourceValue != null)
            {
                return ResourceValue.Equals(other.ResourceValue);
            }
            else if (IsComputed && other.IsComputed)
            {
                return true;
            }
            else if (IsNull && other.IsNull)
            {
                return true;
            }

            return false;
        }

        public override bool Equals(object? obj)
        {
            if (obj is PropertyValue pv)
            {
                return Equals(pv);
            }
            return false;
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(IsSecret, Dependencies);

            if (BoolValue != null) return HashCode.Combine(hash, BoolValue.GetHashCode());
            if (NumberValue != null) return HashCode.Combine(hash, NumberValue.GetHashCode());
            if (StringValue != null) return HashCode.Combine(hash, StringValue.GetHashCode());
            if (ArrayValue != null) return HashCode.Combine(hash, ArrayValue.GetHashCode());
            if (MapValue != null) return HashCode.Combine(hash, MapValue.GetHashCode());
            if (AssetValue != null) return HashCode.Combine(hash, AssetValue.GetHashCode());
            if (ArchiveValue != null) return HashCode.Combine(hash, ArchiveValue.GetHashCode());
            if (ResourceValue != null) return HashCode.Combine(hash, ResourceValue.GetHashCode());
            if (IsComputed) return HashCode.Combine(hash, SpecialType.IsComputed.GetHashCode());
            return HashCode.Combine(hash, SpecialType.IsNull.GetHashCode());
        }

        public T Match<T>(
            Func<T> nullCase,
            Func<bool, T> boolCase,
            Func<double, T> numberCase,
            Func<string, T> stringCase,
            Func<ImmutableArray<PropertyValue>, T> arrayCase,
            Func<ImmutableDictionary<string, PropertyValue>, T> mapCase,
            Func<Asset, T> assetCase,
            Func<Archive, T> archiveCase,
            Func<ResourceReference, T> resourceCase,
            Func<T> computedCase)
        {
            if (BoolValue != null) return boolCase(BoolValue.Value);
            if (NumberValue != null) return numberCase(NumberValue.Value);
            if (StringValue != null) return stringCase(StringValue);
            if (ArrayValue != null) return arrayCase(ArrayValue.Value);
            if (MapValue != null) return mapCase(MapValue);
            if (AssetValue != null) return assetCase(AssetValue);
            if (ArchiveValue != null) return archiveCase(ArchiveValue);
            if (ResourceValue != null) return resourceCase(ResourceValue.Value);
            if (IsComputed) return computedCase();
            return nullCase();
        }

        public bool TryGetBool(out bool value)
        {
            if (BoolValue.HasValue)
            {
                value = BoolValue.Value;
                return true;
            }
            value = default;
            return false;
        }

        public bool TryGetNumber(out double value)
        {
            if (NumberValue.HasValue)
            {
                value = NumberValue.Value;
                return true;
            }
            value = default;
            return false;
        }

        public bool TryGetString([NotNullWhen(true)] out string? value)
        {
            if (StringValue != null)
            {
                value = StringValue;
                return true;
            }
            value = default;
            return false;
        }

        public bool TryGetArray(out ImmutableArray<PropertyValue> value)
        {
            if (ArrayValue.HasValue)
            {
                value = ArrayValue.Value;
                return true;
            }
            value = default;
            return false;
        }

        public bool TryGetMap([NotNullWhen(true)] out ImmutableDictionary<string, PropertyValue>? value)
        {
            if (MapValue != null)
            {
                value = MapValue;
                return true;
            }
            value = default;
            return false;
        }

        public bool TryGetAsset(out Asset? value)
        {
            if (AssetValue != null)
            {
                value = AssetValue;
                return true;
            }
            value = default;
            return false;
        }

        public bool TryGetArchive(out Archive? value)
        {
            if (ArchiveValue != null)
            {
                value = ArchiveValue;
                return true;
            }
            value = default;
            return false;
        }

        public bool TryGetResource(out ResourceReference value)
        {
            if (ResourceValue != null)
            {
                value = ResourceValue.Value;
                return true;
            }
            value = default;
            return false;
        }

        public override string? ToString()
        {
            var val = Match<string?>(
                () => "null",
                b => b.ToString(),
                n => n.ToString(CultureInfo.InvariantCulture),
                s => s,
                a => "[" + String.Join(",", a.Select(i => i.ToString())) + "]",
                o => "{" + String.Join(",", o.Select(i => i.Key + ":" + i.Value.ToString())) + "}",
                asset => asset.ToString(),
                archive => archive.ToString(),
                r => r.ToString(),
                () => "{unknown}"
            );

            if (IsSecret)
            {
                val = $"{val} (secret)";
            }

            if (!Dependencies.IsEmpty)
            {
                val = $"{val} (deps: {string.Join(", ", Dependencies.Select(d => d.ToString()))})";
            }

            return val;
        }

        private enum SpecialType
        {
            IsNull = 0,
            IsComputed,
        }

        public static readonly PropertyValue Null = new PropertyValue(SpecialType.IsNull);
        public static readonly PropertyValue Computed = new PropertyValue(SpecialType.IsComputed);

        public PropertyValue(bool value)
        {
            BoolValue = value;
            IsSecret = false;
            Dependencies = ImmutableHashSet<Urn>.Empty;
        }
        public PropertyValue(double value)
        {
            NumberValue = value;
            IsSecret = false;
            Dependencies = ImmutableHashSet<Urn>.Empty;
        }
        public PropertyValue(string value)
        {
            StringValue = value;
            IsSecret = false;
            Dependencies = ImmutableHashSet<Urn>.Empty;
        }
        public PropertyValue(ImmutableArray<PropertyValue> value)
        {
            ArrayValue = value;
            IsSecret = false;
            Dependencies = ImmutableHashSet<Urn>.Empty;
        }
        public PropertyValue(ImmutableDictionary<string, PropertyValue> value)
        {
            MapValue = value;
            IsSecret = false;
            Dependencies = ImmutableHashSet<Urn>.Empty;
        }
        public PropertyValue(Asset value)
        {
            AssetValue = value;
            IsSecret = false;
            Dependencies = ImmutableHashSet<Urn>.Empty;
        }
        public PropertyValue(Archive value)
        {
            ArchiveValue = value;
            IsSecret = false;
            Dependencies = ImmutableHashSet<Urn>.Empty;
        }
        public PropertyValue(ResourceReference value)
        {
            ResourceValue = value;
            IsSecret = false;
            Dependencies = ImmutableHashSet<Urn>.Empty;
        }

        private PropertyValue(SpecialType type)
        {
            if (type == SpecialType.IsComputed)
            {
                _isComputed = true;
            }
            IsSecret = false;
            Dependencies = ImmutableHashSet<Urn>.Empty;
        }

        private PropertyValue(PropertyValue value, bool isSecret, ImmutableHashSet<Urn> dependencies)
        {
            IsSecret = isSecret;
            Dependencies = dependencies;

            BoolValue = value.BoolValue;
            NumberValue = value.NumberValue;
            StringValue = value.StringValue;
            ArrayValue = value.ArrayValue;
            MapValue = value.MapValue;
            AssetValue = value.AssetValue;
            ArchiveValue = value.ArchiveValue;
            ResourceValue = value.ResourceValue;
            _isComputed = value._isComputed;
        }

        public PropertyValue WithSecret(bool secret)
        {
            return new PropertyValue(this, secret, Dependencies);
        }

        public PropertyValue WithDependencies(ImmutableHashSet<Urn> dependencies)
        {
            return new PropertyValue(this, IsSecret, dependencies);
        }

        static bool TryGetStringValue(Google.Protobuf.Collections.MapField<string, Value> fields, string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? result)
        {
            if (fields.TryGetValue(key, out var value) && value.KindCase == Value.KindOneofCase.StringValue)
            {
                result = value.StringValue;
                return true;
            }
            result = null;
            return false;
        }

        internal static ImmutableDictionary<string, PropertyValue> Unmarshal(Struct properties)
        {
            var builder = ImmutableDictionary.CreateBuilder<string, PropertyValue>();
            foreach (var item in properties.Fields)
            {
                builder.Add(item.Key, Unmarshal(item.Value));
            }
            return builder.ToImmutable();
        }

        internal static PropertyValue Unmarshal(Value value)
        {
            switch (value.KindCase)
            {
                case Value.KindOneofCase.NullValue:
                    return PropertyValue.Null;
                case Value.KindOneofCase.BoolValue:
                    return new PropertyValue(value.BoolValue);
                case Value.KindOneofCase.NumberValue:
                    return new PropertyValue(value.NumberValue);
                case Value.KindOneofCase.StringValue:
                    {
                        // This could be the special unknown value
                        if (value.StringValue == Constants.UnknownValue)
                        {
                            return PropertyValue.Computed;
                        }
                        return new PropertyValue(value.StringValue);
                    }
                case Value.KindOneofCase.ListValue:
                    {
                        var listValue = value.ListValue;
                        var builder = ImmutableArray.CreateBuilder<PropertyValue>(listValue.Values.Count);
                        foreach (var item in listValue.Values)
                        {
                            builder.Add(Unmarshal(item));
                        }
                        return new PropertyValue(builder.ToImmutable());
                    }
                case Value.KindOneofCase.StructValue:
                    {
                        // This could be a plain object, or one of our specials
                        var structValue = value.StructValue;

                        if (TryGetStringValue(structValue.Fields, Constants.SpecialSigKey, out var sig))
                        {
                            switch (sig)
                            {
                                case Constants.SpecialSecretSig:
                                    {
                                        if (!structValue.Fields.TryGetValue(Constants.ValueName, out var secretValue))
                                            throw new InvalidOperationException("Secrets must have a field called 'value'");

                                        return Unmarshal(secretValue).WithSecret(true);
                                    }
                                case Constants.SpecialAssetSig:
                                    {
                                        if (TryGetStringValue(structValue.Fields, Constants.AssetOrArchivePathName, out var path))
                                            return new PropertyValue(new FileAsset(path));

                                        if (TryGetStringValue(structValue.Fields, Constants.AssetOrArchiveUriName, out var uri))
                                            return new PropertyValue(new RemoteAsset(uri));

                                        if (TryGetStringValue(structValue.Fields, Constants.AssetTextName, out var text))
                                            return new PropertyValue(new StringAsset(text));

                                        throw new InvalidOperationException("Value was marked as Asset, but did not conform to required shape.");
                                    }
                                case Constants.SpecialArchiveSig:
                                    {
                                        if (TryGetStringValue(structValue.Fields, Constants.AssetOrArchivePathName, out var path))
                                            return new PropertyValue(new FileArchive(path));

                                        if (TryGetStringValue(structValue.Fields, Constants.AssetOrArchiveUriName, out var uri))
                                            return new PropertyValue(new RemoteArchive(uri));

                                        if (structValue.Fields.TryGetValue(Constants.ArchiveAssetsName, out var assetsValue))
                                        {
                                            if (assetsValue.KindCase == Value.KindOneofCase.StructValue)
                                            {
                                                var assets = ImmutableDictionary.CreateBuilder<string, AssetOrArchive>();
                                                foreach (var (name, val) in assetsValue.StructValue.Fields)
                                                {
                                                    var innerAssetOrArchive = Unmarshal(val);
                                                    if (innerAssetOrArchive.AssetValue != null)
                                                    {
                                                        assets[name] = innerAssetOrArchive.AssetValue;
                                                    }
                                                    else if (innerAssetOrArchive.ArchiveValue != null)
                                                    {
                                                        assets[name] = innerAssetOrArchive.ArchiveValue;
                                                    }
                                                    else
                                                    {
                                                        throw new InvalidOperationException("AssetArchive contained an element that wasn't itself an Asset or Archive.");
                                                    }
                                                }

                                                return new PropertyValue(new AssetArchive(assets.ToImmutable()));
                                            }
                                        }

                                        throw new InvalidOperationException("Value was marked as Archive, but did not conform to required shape.");
                                    }
                                case Constants.SpecialResourceSig:
                                    {
                                        if (!TryGetStringValue(structValue.Fields, Constants.UrnPropertyName, out var urn))
                                        {
                                            throw new InvalidOperationException("Value was marked as a Resource, but did not conform to required shape.");
                                        }

                                        if (!TryGetStringValue(structValue.Fields, Constants.ResourceVersionName, out var version))
                                        {
                                            version = "";
                                        }

                                        PropertyValue? idProperty = null;
                                        if (structValue.Fields.TryGetValue(Constants.IdPropertyName, out var id))
                                        {
                                            idProperty = Unmarshal(id);
                                        }

                                        return new PropertyValue(new ResourceReference(new Urn(urn), idProperty, version));
                                    }
                                case Constants.SpecialOutputValueSig:
                                    {
                                        PropertyValue element = Computed;
                                        if (structValue.Fields.TryGetValue(Constants.ValueName, out var knownElement))
                                        {
                                            element = Unmarshal(knownElement);
                                        }
                                        var secret = false;
                                        if (structValue.Fields.TryGetValue(Constants.SecretName, out var v))
                                        {
                                            if (v.KindCase == Value.KindOneofCase.BoolValue)
                                            {
                                                secret = v.BoolValue;
                                            }
                                            else
                                            {
                                                throw new InvalidOperationException("Value was marked as an Output, but did not conform to required shape.");
                                            }
                                        }

                                        var dependenciesBuilder = ImmutableHashSet.CreateBuilder<Urn>();
                                        if (structValue.Fields.TryGetValue(Constants.DependenciesName, out var dependencies))
                                        {
                                            if (dependencies.KindCase == Value.KindOneofCase.ListValue)
                                            {
                                                foreach (var dependency in dependencies.ListValue.Values)
                                                {
                                                    if (dependency.KindCase == Value.KindOneofCase.StringValue)
                                                    {
                                                        dependenciesBuilder.Add(new Urn(dependency.StringValue));
                                                    }
                                                    else
                                                    {
                                                        throw new InvalidOperationException("Value was marked as an Output, but did not conform to required shape.");
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                throw new InvalidOperationException("Value was marked as an Output, but did not conform to required shape.");
                                            }
                                        }

                                        return element.WithDependencies(dependenciesBuilder.ToImmutable()).WithSecret(secret);
                                    }

                                default:
                                    throw new InvalidOperationException($"Unrecognized special signature: {sig}");
                            }
                        }
                        else
                        {
                            // Just a plain object
                            var builder = ImmutableDictionary.CreateBuilder<string, PropertyValue>();
                            foreach (var item in structValue.Fields)
                            {
                                builder.Add(item.Key, Unmarshal(item.Value));
                            }
                            return new PropertyValue(builder.ToImmutable());
                        }
                    }

                case Value.KindOneofCase.None:
                default:
                    throw new InvalidOperationException($"Unexpected grpc value: {value}");
            }
        }

        internal static Struct Marshal(IDictionary<string, PropertyValue> properties)
        {
            var result = new Struct();
            foreach (var item in properties)
            {
                result.Fields[item.Key] = Marshal(item.Value);
            }
            return result;
        }

        private static Value MarshalAsset(Asset asset)
        {
            var result = new Struct();
            result.Fields[Constants.SpecialSigKey] = Value.ForString(Constants.SpecialAssetSig);
            result.Fields[asset.PropName] = Value.ForString((string)asset.Value);
            return Value.ForStruct(result);
        }

        private static Value MarshalArchive(Archive archive)
        {
            var result = new Struct();
            result.Fields[Constants.SpecialSigKey] = Value.ForString(Constants.SpecialAssetSig);

            if (archive.Value is string str)
            {
                result.Fields[archive.PropName] = Value.ForString(str);
            }
            else
            {
                var inner = (ImmutableDictionary<string, AssetOrArchive>)archive.Value;
                var innerStruct = new Struct();
                foreach (var item in inner)
                {
                    innerStruct.Fields[item.Key] = MarshalAssetOrArchive(item.Value);
                }
                result.Fields[archive.PropName] = Value.ForStruct(innerStruct);
            }
            return Value.ForStruct(result);
        }

        private static Value MarshalAssetOrArchive(AssetOrArchive assetOrArchive)
        {
            if (assetOrArchive is Asset asset)
            {
                return MarshalAsset(asset);
            }
            else if (assetOrArchive is Archive archive)
            {
                return MarshalArchive(archive);
            }
            throw new InvalidOperationException("Internal error, AssetOrArchive was neither an Asset or Archive");
        }

        internal static Value Marshal(PropertyValue value)
        {
            var val = value.Match<Value>(
                () => Value.ForNull(),
                b => Value.ForBool(b),
                n => Value.ForNumber(n),
                s => Value.ForString(s),
                a =>
                {
                    var result = new Value[a.Length];
                    for (int i = 0; i < a.Length; ++i)
                    {
                        result[i] = Marshal(a[i]);
                    }
                    return Value.ForList(result);
                },
                o =>
                {
                    var result = new Struct();
                    foreach (var item in o)
                    {
                        result.Fields[item.Key] = Marshal(item.Value);
                    }
                    return Value.ForStruct(result);
                },
                asset => MarshalAsset(asset),
                archive => MarshalArchive(archive),
                resource =>
                {
                    var result = new Struct();
                    result.Fields[Constants.SpecialSigKey] = Value.ForString(Constants.SpecialResourceSig);
                    result.Fields[Constants.UrnPropertyName] = Value.ForString(resource.URN);
                    if (resource.Id != null)
                    {
                        result.Fields[Constants.IdPropertyName] = Marshal(resource.Id);
                    }
                    if (resource.PackageVersion != "")
                    {
                        result.Fields[Constants.ResourceVersionName] = Value.ForString(resource.PackageVersion);
                    }
                    return Value.ForStruct(result);
                },
                () => Value.ForString(Constants.UnknownValue)
            );

            if (!value.Dependencies.IsEmpty)
            {
                var result = new Struct();
                result.Fields[Constants.SpecialSigKey] = Value.ForString(Constants.SpecialOutputValueSig);
                result.Fields[Constants.ValueName] = val;

                var deps = new Value[value.Dependencies.Count];
                var i = 0;
                foreach (var dependency in value.Dependencies)
                {
                    deps[i++] = Value.ForString(dependency);
                }
                result.Fields[Constants.DependenciesName] = Value.ForList(deps);
                if (value.IsSecret)
                {
                    result.Fields[Constants.SecretName] = Value.ForBool(true);
                }

                return Value.ForStruct(result);
            }

            if (value.IsSecret)
            {
                var result = new Struct();
                result.Fields[Constants.SpecialSigKey] = Value.ForString(Constants.SpecialSecretSig);
                result.Fields[Constants.ValueName] = val;
                return Value.ForStruct(result);
            }

            return val;
        }
    }
}

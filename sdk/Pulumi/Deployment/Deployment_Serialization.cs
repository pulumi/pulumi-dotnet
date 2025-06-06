// Copyright 2016-2019, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Pulumi.Serialization;

namespace Pulumi
{
    public partial class Deployment
    {
        // Set to true to enable excessive debug output. This is useful for debugging.
        internal static bool _excessiveDebugOutput;

        /// <summary>
        /// <see cref="SerializeResourcePropertiesAsync"/> walks the props object passed in,
        /// awaiting all interior promises besides those for <see cref="Resource.Urn"/> and <see
        /// cref="CustomResource.Id"/>, creating a reasonable POCO object that can be remoted over
        /// to registerResource.
        /// </summary>
        private static Task<SerializationResult> SerializeResourcePropertiesAsync(
            string label, IDictionary<string, object?> args, bool keepResources, bool keepOutputValues,
            bool excludeResourceReferencesFromDependencies = false)
        {
            return SerializeFilteredPropertiesAsync(
                label, args,
                key => key != Constants.IdPropertyName && key != Constants.UrnPropertyName,
                keepResources, keepOutputValues, excludeResourceReferencesFromDependencies);
        }

        private static async Task<Struct> SerializeAllPropertiesAsync(
            string label, IDictionary<string, object?> args, bool keepResources, bool keepOutputValues = false,
            bool excludeResourceReferencesFromDependencies = false)
        {
            var result = await SerializeFilteredPropertiesAsync(
                label, args, _ => true,
                keepResources, keepOutputValues, excludeResourceReferencesFromDependencies).ConfigureAwait(false);
            return result.Serialized;
        }

        /// <summary>
        /// <see cref="SerializeFilteredPropertiesAsync"/> walks the props object passed in,
        /// awaiting all interior promises for properties with keys that match the provided filter,
        /// creating a reasonable POCO object that can be remoted over to registerResource.
        /// </summary>
        ///
        /// <param name="label">label</param>
        /// <param name="args">args</param>
        /// <param name="acceptKey">acceptKey</param>
        /// <param name="keepResources">keepResources</param>
        /// <param name="keepOutputValues">
        /// Specifies if we should marshal output values. It is the callers
        /// responsibility to ensure that the monitor supports the OutputValues
        /// feature.
        /// </param>
        /// <param name="excludeResourceReferencesFromDependencies">
        /// Specifies if we should exclude resource references from the resulting
        /// <see cref="SerializationResult.PropertyToDependentResources"/>. This is useful for remote
        /// components (i.e. multi-lang components, or MLCs) where we want property dependencies to be
        /// empty for a property that only contains resource references.
        /// </param>
        private static async Task<SerializationResult> SerializeFilteredPropertiesAsync(
            string label, IDictionary<string, object?> args, Predicate<string> acceptKey, bool keepResources, bool keepOutputValues,
            bool excludeResourceReferencesFromDependencies = false)
        {
            var result = await SerializeFilteredPropertiesRawAsync(label, args, acceptKey, keepResources, keepOutputValues,
                excludeResourceReferencesFromDependencies);
            return result.ToSerializationResult();
        }

        /// <summary>
        /// Acts as `SerializeFilteredPropertiesAsync` without the
        /// last step of encoding the value into a Protobuf form.
        /// </summary>
        private static async Task<RawSerializationResult> SerializeFilteredPropertiesRawAsync(
            string label, IDictionary<string, object?> args, Predicate<string> acceptKey, bool keepResources, bool keepOutputValues,
            bool excludeResourceReferencesFromDependencies = false)
        {
            var propertyToDependentResources = ImmutableDictionary.CreateBuilder<string, HashSet<Resource>>();
            var result = ImmutableDictionary.CreateBuilder<string, object>();

            foreach (var (key, val) in args)
            {
                if (acceptKey(key))
                {
                    // We treat properties with null values as if they do not exist.
                    var serializer = new Serializer(_excessiveDebugOutput);
                    var v = await serializer.SerializeAsync($"{label}.{key}", val, keepResources, keepOutputValues,
                        excludeResourceReferencesFromDependencies).ConfigureAwait(false);
                    if (v != null)
                    {
                        result[key] = v;
                        propertyToDependentResources[key] = serializer.DependentResources;
                    }
                }
            }

            return new RawSerializationResult(
                result.ToImmutable(),
                propertyToDependentResources.ToImmutable());
        }

        private readonly struct SerializationResult
        {
            public readonly Struct Serialized;
            public readonly ImmutableDictionary<string, HashSet<Resource>> PropertyToDependentResources;

            public SerializationResult(
                Struct result,
                ImmutableDictionary<string, HashSet<Resource>> propertyToDependentResources)
            {
                Serialized = result;
                PropertyToDependentResources = propertyToDependentResources;
            }

            public void Deconstruct(
                out Struct serialized,
                out ImmutableDictionary<string, HashSet<Resource>> propertyToDependentResources)
            {
                serialized = Serialized;
                propertyToDependentResources = PropertyToDependentResources;
            }
        }

        private readonly struct RawSerializationResult
        {
            public readonly ImmutableDictionary<string, object> PropertyValues;
            public readonly ImmutableDictionary<string, HashSet<Resource>> PropertyToDependentResources;

            public RawSerializationResult(
                ImmutableDictionary<string, object> propertyValues,
                ImmutableDictionary<string, HashSet<Resource>> propertyToDependentResources)
            {
                PropertyValues = propertyValues;
                PropertyToDependentResources = propertyToDependentResources;
            }

            public SerializationResult ToSerializationResult()
                => new SerializationResult(
                    Serializer.CreateStruct(PropertyValues!),
                    PropertyToDependentResources);
        }
    }
}

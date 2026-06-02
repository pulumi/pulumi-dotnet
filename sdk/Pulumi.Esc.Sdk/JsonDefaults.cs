// Copyright 2024, Pulumi Corporation.  All rights reserved.

using System;
using System.Text.Json;

namespace Pulumi.Esc.Sdk
{
    /// <summary>
    /// Provides a JsonSerializerOptions instance that works on both .NET 6 and .NET 7+ runtimes.
    /// On .NET 7+ (including .NET 10), reflection-based serialization may be disabled by default.
    /// This helper ensures TypeInfoResolver is set when available.
    /// </summary>
    internal static class JsonDefaults
    {
        private static readonly Lazy<JsonSerializerOptions> _options = new(CreateOptions);

        /// <summary>
        /// Gets a shared JsonSerializerOptions instance safe for use on any .NET runtime.
        /// </summary>
        public static JsonSerializerOptions Options => _options.Value;

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions();
            EnsureTypeInfoResolver(options);
            return options;
        }

        /// <summary>
        /// Ensures the given JsonSerializerOptions has a TypeInfoResolver set,
        /// if running on a .NET 7+ runtime where the property exists.
        /// Uses reflection so this compiles and works against net6.0.
        /// </summary>
        internal static void EnsureTypeInfoResolver(JsonSerializerOptions options)
        {
            var resolverProp = typeof(JsonSerializerOptions).GetProperty("TypeInfoResolver");
            if (resolverProp != null && resolverProp.GetValue(options) == null)
            {
                var resolverType = Type.GetType(
                    "System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver, System.Text.Json");
                if (resolverType != null)
                {
                    resolverProp.SetValue(options, Activator.CreateInstance(resolverType));
                }
            }
        }
    }
}

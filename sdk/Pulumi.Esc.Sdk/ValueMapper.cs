// Copyright 2024, Pulumi Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json;
using Pulumi.Esc.Sdk.Model;

namespace Pulumi.Esc.Sdk
{
    /// <summary>
    /// Provides utilities for converting <see cref="Value"/> trees into plain .NET objects.
    /// </summary>
    public static class ValueMapper
    {
        /// <summary>
        /// Extracts the resolved values from a <see cref="ModelEnvironment"/> as a flat dictionary
        /// of property name to plain .NET object (primitives, dictionaries, lists).
        /// </summary>
        /// <param name="environment">The resolved environment.</param>
        /// <returns>A dictionary of property names to their resolved primitive values, or null if the environment has no properties.</returns>
        public static Dictionary<string, object?>? MapValues(ModelEnvironment? environment)
        {
            if (environment?.Properties == null)
                return null;

            var result = new Dictionary<string, object?>(environment.Properties.Count);
            foreach (var kvp in environment.Properties)
            {
                result[kvp.Key] = MapValuePrimitive(kvp.Value);
            }
            return result;
        }

        /// <summary>
        /// Recursively unwraps a <see cref="Value"/> into a plain .NET object.
        /// <list type="bullet">
        ///   <item>Scalar values (string, bool, number) are returned as-is.</item>
        ///   <item>Object values become <c>Dictionary&lt;string, object?&gt;</c>.</item>
        ///   <item>Array values become <c>List&lt;object?&gt;</c>.</item>
        /// </list>
        /// The <see cref="Value.Trace"/>, <see cref="Value.Secret"/>, and <see cref="Value.Unknown"/> metadata is stripped.
        /// </summary>
        /// <param name="value">The value to unwrap.</param>
        /// <returns>The unwrapped plain object.</returns>
        public static object? MapValuePrimitive(Value? value)
        {
            if (value == null)
                return null;

            return UnwrapPrimitive(value.VarValue);
        }

        /// <summary>
        /// Recursively unwraps an arbitrary object that may contain nested <see cref="Value"/> objects,
        /// JSON elements, dictionaries, or arrays into plain .NET objects.
        /// </summary>
        /// <param name="obj">The object to unwrap.</param>
        /// <returns>The unwrapped plain object.</returns>
        public static object? UnwrapPrimitive(object? obj)
        {
            if (obj == null)
                return null;

            // Handle Value objects directly
            if (obj is Value val)
                return UnwrapPrimitive(val.VarValue);

            // Handle JsonElement (from System.Text.Json deserialization)
            if (obj is JsonElement jsonElement)
                return UnwrapJsonElement(jsonElement);

            // Handle dictionaries
            if (obj is IDictionary<string, object?> dict)
            {
                var result = new Dictionary<string, object?>(dict.Count);
                foreach (var kvp in dict)
                {
                    result[kvp.Key] = UnwrapPrimitive(kvp.Value);
                }
                return result;
            }

            if (obj is IDictionary<string, Value> valueDict)
            {
                var result = new Dictionary<string, object?>(valueDict.Count);
                foreach (var kvp in valueDict)
                {
                    result[kvp.Key] = MapValuePrimitive(kvp.Value);
                }
                return result;
            }

            // Handle lists/arrays
            if (obj is IList<object?> list)
            {
                var result = new List<object?>(list.Count);
                foreach (var item in list)
                {
                    result.Add(UnwrapPrimitive(item));
                }
                return result;
            }

            // Scalar: string, bool, int, double, etc.
            return obj;
        }

        private static object? UnwrapJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var longVal))
                        return longVal;
                    return element.GetDouble();

                case JsonValueKind.Array:
                    var arr = new List<object?>();
                    foreach (var item in element.EnumerateArray())
                    {
                        arr.Add(UnwrapJsonElement(item));
                    }
                    return arr;

                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object?>();
                    foreach (var prop in element.EnumerateObject())
                    {
                        dict[prop.Name] = UnwrapJsonElement(prop.Value);
                    }
                    return dict;

                default:
                    return element.ToString();
            }
        }
    }
}

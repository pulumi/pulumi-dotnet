// Copyright 2024, Pulumi Corporation.  All rights reserved.

using System.Collections.Generic;
using System.Text.Json;
using Pulumi.Esc.Sdk.Client;
using Pulumi.Esc.Sdk.Model;
using Xunit;

namespace Pulumi.Esc.Sdk.Tests
{
    /// <summary>
    /// Unit tests for <see cref="ValueMapper"/>.
    /// </summary>
    public class ValueMapperTests
    {
        // Helper to create a Value with a dummy Trace (required by the generated constructor).
        private static Value MakeValue(object? varValue)
        {
            var trace = new Trace();
            return new Value(trace, varValue: varValue);
        }

        [Fact]
        public void MapValuePrimitive_Null_ReturnsNull()
        {
            Assert.Null(ValueMapper.MapValuePrimitive(null));
        }

        [Fact]
        public void MapValuePrimitive_StringValue()
        {
            var value = MakeValue("hello");
            Assert.Equal("hello", ValueMapper.MapValuePrimitive(value));
        }

        [Fact]
        public void MapValuePrimitive_BoolValue()
        {
            var value = MakeValue(true);
            Assert.Equal(true, ValueMapper.MapValuePrimitive(value));
        }

        [Fact]
        public void MapValuePrimitive_NumberValue()
        {
            var value = MakeValue(42.0);
            Assert.Equal(42.0, ValueMapper.MapValuePrimitive(value));
        }

        [Fact]
        public void MapValuePrimitive_NestedDictionary()
        {
            var inner = new Dictionary<string, Value>
            {
                ["key1"] = MakeValue("value1"),
                ["key2"] = MakeValue(123.0),
            };
            var value = MakeValue(inner);

            var result = ValueMapper.MapValuePrimitive(value) as Dictionary<string, object?>;
            Assert.NotNull(result);
            Assert.Equal("value1", result!["key1"]);
            Assert.Equal(123.0, result["key2"]);
        }

        [Fact]
        public void MapValuePrimitive_Array()
        {
            var items = new List<object?> { "a", 1.0, true };
            var value = MakeValue(items);

            var result = ValueMapper.MapValuePrimitive(value) as List<object?>;
            Assert.NotNull(result);
            Assert.Equal(3, result!.Count);
            Assert.Equal("a", result[0]);
            Assert.Equal(1.0, result[1]);
            Assert.Equal(true, result[2]);
        }

        [Fact]
        public void MapValues_NullEnvironment_ReturnsNull()
        {
            Assert.Null(ValueMapper.MapValues(null));
        }

        [Fact]
        public void MapValues_EmptyProperties_ReturnsNull()
        {
            var env = new ModelEnvironment();
            Assert.Null(ValueMapper.MapValues(env));
        }

        [Fact]
        public void MapValues_WithProperties()
        {
            var env = new ModelEnvironment
            {
                Properties = new Dictionary<string, Value>
                {
                    ["foo"] = MakeValue("bar"),
                    ["count"] = MakeValue(42.0),
                    ["flag"] = MakeValue(true),
                }
            };

            var result = ValueMapper.MapValues(env);
            Assert.NotNull(result);
            Assert.Equal("bar", result!["foo"]);
            Assert.Equal(42.0, result["count"]);
            Assert.Equal(true, result["flag"]);
        }

        [Fact]
        public void UnwrapPrimitive_JsonElement_Object()
        {
            var json = "{\"name\":\"test\",\"value\":42}";
            var element = JsonSerializer.Deserialize<JsonElement>(json);

            var result = ValueMapper.UnwrapPrimitive(element) as Dictionary<string, object?>;
            Assert.NotNull(result);
            Assert.Equal("test", result!["name"]);
            Assert.Equal(42L, result["value"]); // integers come as long
        }

        [Fact]
        public void UnwrapPrimitive_JsonElement_Array()
        {
            var json = "[1,2,3]";
            var element = JsonSerializer.Deserialize<JsonElement>(json);

            var result = ValueMapper.UnwrapPrimitive(element) as List<object?>;
            Assert.NotNull(result);
            Assert.Equal(3, result!.Count);
            Assert.Equal(1L, result[0]);
            Assert.Equal(2L, result[1]);
            Assert.Equal(3L, result[2]);
        }

        [Fact]
        public void UnwrapPrimitive_JsonElement_Null()
        {
            var json = "null";
            var element = JsonSerializer.Deserialize<JsonElement>(json);

            Assert.Null(ValueMapper.UnwrapPrimitive(element));
        }

        [Fact]
        public void UnwrapPrimitive_JsonElement_Boolean()
        {
            var json = "true";
            var element = JsonSerializer.Deserialize<JsonElement>(json);

            Assert.Equal(true, ValueMapper.UnwrapPrimitive(element));
        }

        [Fact]
        public void UnwrapPrimitive_JsonElement_String()
        {
            var json = "\"hello\"";
            var element = JsonSerializer.Deserialize<JsonElement>(json);

            Assert.Equal("hello", ValueMapper.UnwrapPrimitive(element));
        }

        [Fact]
        public void UnwrapPrimitive_DeeplyNested()
        {
            var innerDict = new Dictionary<string, object?>
            {
                ["deep"] = "value",
            };
            var outerDict = new Dictionary<string, object?>
            {
                ["level1"] = innerDict,
            };

            var result = ValueMapper.UnwrapPrimitive(outerDict) as Dictionary<string, object?>;
            Assert.NotNull(result);

            var level1 = result!["level1"] as Dictionary<string, object?>;
            Assert.NotNull(level1);
            Assert.Equal("value", level1!["deep"]);
        }
    }
}

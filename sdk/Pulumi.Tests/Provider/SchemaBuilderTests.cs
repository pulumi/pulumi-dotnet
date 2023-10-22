using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Pulumi.Experimental.Provider;
using Xunit;

namespace Pulumi.Tests.Provider;

public class SchemaBuilderTests
{
    class TestArgs : ResourceArgs
    {
        public int IntProperty { get; set; }
        public string StringProperty { get; set; }
        public bool BoolProperty { get; set; }
        public double DoubleProperty { get; set; }
        public string[] StringArrayProperty { get; set; }
        public List<string> StringListProperty { get; set; }
        public ImmutableArray<string> StringImmutableArrayProperty { get; set; }
        public Dictionary<string, string> StringDictionaryProperty { get; set; }
        public ImmutableDictionary<string, string> StringImmutableDictionaryProperty { get; set; }
        public Input<string> StringInputProperty { get; set; }
        public InputList<string> StringInputListProperty { get; set; }
        public InputMap<string> StringInputMapProperty { get; set; }
        [Secret]
        public Input<string> SecretStringInputProperty { get; set; }
        public int? OptionalIntProperty { get; set; }
        public Input<int?> OptionalIntInputProperty { get; set; }
        [Input("overriddenStringProperty")]
        public string OverriddenStringProperty { get; set; }
    }

    class TestComponent : ComponentResource
    {
        public TestComponent(string name) : base("token:token:token", name, ResourceArgs.Empty)
        {

        }
    }

    [Fact]
    public async Task BuildingSchemaFromTypesWorks()
    {
        var schema =
            new ComponentProviderBuilder()
                .RegisterComponent<TestArgs, TestComponent>("token:token:token",
                    (request, args) => new TestComponent(request.Name))
                .BuildSchema();

        Assert.True(schema.ContainsKey("resources") && schema["resources"].Type == JTokenType.Object);

        var resources = schema["resources"] as JObject;

        if (resources == null)
        {
            throw new Exception("Expected resources to be an object");
        }

        Assert.Equal(1, resources.Count);
        var testComponent = resources["token:token:token"] as JObject;
        if (testComponent == null)
        {
            throw new Exception("Expected testComponent to be an object");
        }

        var expectedInputProperties = new JObject
        {
            ["IntProperty"] = new JObject
            {
                ["type"] = "integer",
                ["plain"] = true
            },
            ["StringProperty"] = new JObject
            {
                ["type"] = "string",
                ["plain"] = true
            },
            ["BoolProperty"] = new JObject
            {
                ["type"] = "bool",
                ["plain"] = true
            },
            ["DoubleProperty"] = new JObject
            {
                ["type"] = "number",
                ["plain"] = true
            },
            ["StringArrayProperty"] = new JObject
            {
                ["type"] = "array",
                ["items"] = new JObject
                {
                    ["type"] = "string"
                },
                ["plain"] = true
            },
            ["StringListProperty"] = new JObject
            {
                ["type"] = "array",
                ["items"] = new JObject
                {
                    ["type"] = "string"
                },
                ["plain"] = true
            },
            ["StringImmutableArrayProperty"] = new JObject
            {
                ["type"] = "array",
                ["items"] = new JObject
                {
                    ["type"] = "string"
                },
                ["plain"] = true
            },
            ["StringDictionaryProperty"] = new JObject
            {
                ["type"] = "object",
                ["additionalProperties"] = new JObject
                {
                    ["type"] = "string"
                },
                ["plain"] = true
            },
            ["StringImmutableDictionaryProperty"] = new JObject
            {
                ["type"] = "object",
                ["additionalProperties"] = new JObject
                {
                    ["type"] = "string"
                },
                ["plain"] = true
            },
            ["StringInputProperty"] = new JObject
            {
                ["type"] = "string"
            },
            ["StringInputListProperty"] = new JObject
            {
                ["type"] = "array",
                ["items"] = new JObject
                {
                    ["type"] = "string"
                }
            },
            ["StringInputMapProperty"] = new JObject
            {
                ["type"] = "object",
                ["additionalProperties"] = new JObject
                {
                    ["type"] = "string"
                }
            },
            ["SecretStringInputProperty"] = new JObject
            {
                ["type"] = "string",
                ["secret"] = true
            },
            ["OptionalIntProperty"] = new JObject
            {
                ["type"] = "integer",
                ["plain"] = true
            },
            ["OptionalIntInputProperty"] = new JObject
            {
                ["type"] = "integer"
            },
            ["overriddenStringProperty"] = new JObject
            {
                ["type"] = "string",
                ["plain"] = true,
                ["language"] = new JObject
                {
                    ["csharp"] = new JObject
                    {
                        ["name"] = "OverriddenStringProperty"
                    }
                }
            }
        };

        var actualInputProperties = testComponent["inputProperties"] as JObject;
        if (actualInputProperties == null)
        {
            throw new Exception("Expected inputProperties to be an object");
        }

        foreach (var property in expectedInputProperties.Properties())
        {
            var propertyName = property.Name;
            if (!expectedInputProperties.ContainsKey(propertyName))
            {
                throw new Exception($"Unexpected property {propertyName}");
            }

            var actualProperty = actualInputProperties[propertyName] as JObject;
            var expectedProperty = expectedInputProperties[propertyName] as JObject;

            if (expectedProperty.ToString() != actualProperty.ToString())
            {
                throw new Exception($"Expected property {propertyName} to be {expectedProperty} but was {actualProperty}");
            }
        }

        foreach (var property in actualInputProperties)
        {
            if (!expectedInputProperties.ContainsKey(property.Key))
            {
                throw new Exception($"Unexpected property {property.Key}");
            }
        }

        var expectedRequiredInputProperties = new string[]
        {
            "IntProperty", "StringProperty", "BoolProperty", "DoubleProperty",
            "StringArrayProperty", "StringListProperty", "StringImmutableArrayProperty",
            "StringDictionaryProperty", "StringImmutableDictionaryProperty",
            "StringInputProperty", "StringInputListProperty", "StringInputMapProperty",
            "SecretStringInputProperty", "overriddenStringProperty"
        };

        var actualRequiredInputProperties = testComponent["requiredInputs"]?.ToObject<string[]>() ?? new string[] { };

        if (actualRequiredInputProperties == null)
        {
            throw new Exception("expected actual required input properties to be non-null");
        }

        foreach (var expectedRequiredInput in expectedRequiredInputProperties)
        {
            if (!actualRequiredInputProperties.Contains(expectedRequiredInput))
            {
                throw new Exception($"Missing expected required input {expectedRequiredInput}");
            }
        }

        foreach (var actualRequiredInput in actualRequiredInputProperties)
        {
            if (!expectedRequiredInputProperties.Contains(actualRequiredInput))
            {
                throw new Exception($"Unexpected required input {actualRequiredInput}");
            }
        }
    }
}

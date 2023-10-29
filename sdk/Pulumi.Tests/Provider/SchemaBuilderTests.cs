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
    class NestedObjectInput
    {
        public Input<int> Int { get; set; }
        public Input<string> String { get; set; }
    }

    class ObjectInput
    {
        public Input<NestedObjectInput> Nested { get; set; }
    }

    enum TestEnum
    {
        Allow,
        Default
    }

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
        public Input<ObjectInput> ObjectInput { get; set; }
        public Input<TestEnum?> EnumInput { get; set; }
    }

    class TestComponent : ComponentResource
    {
        public Output<string> StringOutput { get; set; }
        public Output<int> IntOutput { get; set; }
        public Output<bool> BoolOutput { get; set; }
        public Output<double> DoubleOutput { get; set; }
        public Output<string[]> StringArrayOutput { get; set; }
        public Output<List<string>> StringListOutput { get; set; }
        public Output<ImmutableArray<string>> StringImmutableArrayOutput { get; set; }
        public Output<Dictionary<string, string>> StringDictionaryOutput { get; set; }
        public Output<ImmutableDictionary<string, string>> StringImmutableDictionaryOutput { get; set; }
        [Output("overriddenString")]
        public Output<string> OverriddenStringOutput { get; set; }

        public TestComponent(string name) : base("token:token:token", name, ResourceArgs.Empty)
        {

        }
    }

    [Fact]
    public async Task BuildingSchemaFromTypesWorks()
    {
        var schema =
            new SimpleProviderBuilder("test")
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
            },
            ["ObjectInput"] = new JObject
            {
                ["$ref"] = "#/types/test::ObjectInput"
            },
            ["EnumInput"] = new JObject
            {
                ["$ref"] = "#/types/test::TestEnum"
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
                throw new Exception($"Unexpected input property {propertyName}");
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
                throw new Exception($"Unexpected input property {property.Key}");
            }
        }

        var expectedRequiredInputProperties = new string[]
        {
            "StringProperty", "IntProperty", "BoolProperty", "DoubleProperty",
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

        var expectedOutputProperties = new JObject
        {
            ["StringOutput"] = new JObject
            {
                ["type"] = "string"
            },
            ["IntOutput"] = new JObject
            {
                ["type"] = "integer"
            },
            ["BoolOutput"] = new JObject
            {
                ["type"] = "bool"
            },
            ["DoubleOutput"] = new JObject
            {
                ["type"] = "number"
            },
            ["StringArrayOutput"] = new JObject
            {
                ["type"] = "array",
                ["items"] = new JObject
                {
                    ["type"] = "string"
                }
            },
            ["StringListOutput"] = new JObject
            {
                ["type"] = "array",
                ["items"] = new JObject
                {
                    ["type"] = "string"
                }
            },
            ["StringImmutableArrayOutput"] = new JObject
            {
                ["type"] = "array",
                ["items"] = new JObject
                {
                    ["type"] = "string"
                }
            },
            ["StringDictionaryOutput"] = new JObject
            {
                ["type"] = "object",
                ["additionalProperties"] = new JObject
                {
                    ["type"] = "string"
                }
            },
            ["StringImmutableDictionaryOutput"] = new JObject
            {
                ["type"] = "object",
                ["additionalProperties"] = new JObject
                {
                    ["type"] = "string"
                }
            },
            ["overriddenString"] = new JObject
            {
                ["type"] = "string",
                ["language"] = new JObject
                {
                    ["csharp"] = new JObject
                    {
                        ["name"] = "OverriddenStringOutput"
                    }
                }
            }
        };


        var actualOutputProperties = testComponent["properties"] as JObject;
        if (actualOutputProperties == null)
        {
            throw new Exception("Expected properties to be an object");
        }

        foreach (var property in expectedOutputProperties.Properties())
        {
            var propertyName = property.Name;
            if (!expectedOutputProperties.ContainsKey(propertyName))
            {
                throw new Exception($"Unexpected output property {propertyName}");
            }

            var actualProperty = actualOutputProperties[propertyName] as JObject;
            var expectedProperty = expectedOutputProperties[propertyName] as JObject;

            if (expectedProperty.ToString() != actualProperty.ToString())
            {
                throw new Exception($"Expected property {propertyName} to be {expectedProperty} but was {actualProperty}");
            }
        }

        foreach (var property in actualOutputProperties)
        {
            if (!expectedOutputProperties.ContainsKey(property.Key))
            {
                throw new Exception($"Unexpected output property {property.Key}");
            }
        }

        var expectedTypes = new JObject
        {
            ["test::NestedObjectInput"] = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["Int"] = new JObject
                    {
                        ["type"] = "integer"
                    },
                    ["String"] = new JObject
                    {
                        ["type"] = "string"
                    }
                },
                ["required"] = new JArray { "Int", "String" }
            },
            ["test::ObjectInput"] = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["Nested"] = new JObject
                    {
                        ["$ref"] = "#/types/test::NestedObjectInput"
                    }
                },
                ["required"] = new JArray()
            },
            ["test::TestEnum"] = new JObject
            {
                ["type"] = "string",
                ["enum"] = new JArray { "Allow", "Default" }
            }
        };

        var actualTypes = schema["types"] as JObject;
        if (actualTypes == null)
        {
            throw new Exception("expected types to be an object");
        }

        foreach (var expectedType in expectedTypes)
        {
            if (!actualTypes.ContainsKey(expectedType.Key))
            {
                throw new Exception($"Missing expected type {expectedType.Key}");
            }

            var actualType = actualTypes[expectedType.Key];
            Assert.Equal(expectedType.Value.ToString(), actualType.ToString());
        }

    }
}

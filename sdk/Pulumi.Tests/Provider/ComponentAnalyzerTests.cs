// Copyright 2025, Pulumi Corporation

namespace Pulumi.Tests.Provider;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

using Pulumi.Experimental.Provider;

public class ComponentAnalyzerTests
{
    [Fact]
    public void TestGenerateSchemaWithMetadata()
    {
        var metadata = new Metadata("test");
        var schema = ComponentAnalyzer.GenerateSchema(metadata, typeof(SelfSignedCertificate));

        // Only verify the package name and resource type prefix
        Assert.Equal("test", schema.Name);
        Assert.Equal("test", schema.DisplayName);
        Assert.Equal("", schema.Version);
        Assert.Single(schema.Resources);
        Assert.Contains("test:index:SelfSignedCertificate", schema.Resources.Keys);
    }

    [Fact]
    public void TestGenerateSchemaWithNoClasses()
    {
        var metadata = new Metadata("test");
        var exception = Assert.Throws<ArgumentException>(() =>
            ComponentAnalyzer.GenerateSchema(metadata, Array.Empty<Type>()));

        Assert.Equal("At least one component type must be provided", exception.Message);
    }

    class SelfSignedCertificateArgs : ResourceArgs
    {
        [Input("algorithm", required: true)]
        public Input<string> Algorithm { get; set; } = null!;

        [Input("ecdsaCurve")]
        public Input<string>? EcdsaCurve { get; set; }

        [Input("bits")]
        public Input<int>? Bits { get; set; }
    }

    class SelfSignedCertificate : ComponentResource
    {
        [Output("pem")]
        public Output<string> Pem { get; private set; } = null!;

        [Output("privateKey")]
        public Output<string> PrivateKey { get; private set; } = null!;

        [Output("caCert")]
        public Output<string?> CaCert { get; private set; } = null!;

        public SelfSignedCertificate(string name, SelfSignedCertificateArgs args, ComponentResourceOptions? options = null)
            : base("pkg:index:SelfSignedCertificate", name, args, options)
        {
            // Implementation not needed for schema testing
        }
    }

    [Fact]
    public void TestAnalyzeComponent()
    {
        var schema = ComponentAnalyzer.GenerateSchema(_metadata, typeof(SelfSignedCertificate));

        var resources = new Dictionary<string, ResourceSpec>();
        resources.Add("my-component:index:SelfSignedCertificate", new ResourceSpec(
            new Dictionary<string, PropertySpec>
            {
                ["algorithm"] = PropertySpec.String,
                ["ecdsaCurve"] = PropertySpec.String,
                ["bits"] = PropertySpec.Integer
            },
            new HashSet<string> { "algorithm" },
            new Dictionary<string, PropertySpec>
            {
                ["pem"] = PropertySpec.String,
                ["privateKey"] = PropertySpec.String,
                ["caCert"] = PropertySpec.String
            },
            new HashSet<string> { "pem", "privateKey" }));

        var expected = CreateBasePackageSpec(resources);
        AssertSchemaEqual(expected, schema);
    }

    class NoArgsComponent : ComponentResource
    {
        public NoArgsComponent()
            : base("my-component:index:NoArgsComponent", "test")
        {
        }

        public NoArgsComponent(string name)
            : base("my-component:index:NoArgsComponent", name)
        {
        }

        public NoArgsComponent(string name, EmptyArgs args)
            : base("my-component:index:NoArgsComponent", name, args)
        {
        }

        public NoArgsComponent(string name, ComponentResourceOptions? options = null)
            : base("my-component:index:NoArgsComponent", name, ResourceArgs.Empty, options)
        {
        }
    }

    [Fact]
    public void TestAnalyzeComponentNoArgs()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ComponentAnalyzer.GenerateSchema(_metadata, typeof(NoArgsComponent)));

        Assert.Equal(
            $"Component {nameof(NoArgsComponent)} must have a constructor with exactly three parameters: " +
            "a string name, a parameter that extends ResourceArgs, and ComponentResourceOptions",
            exception.Message);
    }

    class EmptyArgs : ResourceArgs
    {
        // Empty args class
    }

    class EmptyComponent : ComponentResource
    {
        public EmptyComponent(string name, EmptyArgs args, ComponentResourceOptions? options = null)
            : base("my-component:index:EmptyComponent", name, args, options)
        {
        }
    }

    [Fact]
    public void TestAnalyzeComponentEmpty()
    {
        var schema = ComponentAnalyzer.GenerateSchema(_metadata, typeof(EmptyComponent));

        var resources = new Dictionary<string, ResourceSpec>();
        resources.Add("my-component:index:EmptyComponent",
            new ResourceSpec(
                new Dictionary<string, PropertySpec>(),
                new HashSet<string>(),
                new Dictionary<string, PropertySpec>(),
                new HashSet<string>()));

        var expected = CreateBasePackageSpec(resources);
        AssertSchemaEqual(expected, schema);
    }

    class ComplexTypeArgs : ResourceArgs
    {
        [Input("aStr", required: true)]
        public string AStr { get; set; } = null!;

        [Input("aListStr", required: false)]
        public List<string>? AListStr { get; set; }

        [Input("aDictStr", required: false)]
        public Dictionary<string, string>? ADictStr { get; set; }
    }

    class PlainTypesArgs : ResourceArgs
    {
        [Input("aInt", required: true)]
        public int AInt { get; set; }

        [Input("aStr", required: true)]
        public string AStr { get; set; } = null!;

        [Input("aFloat", required: true)]
        public double AFloat { get; set; }

        [Input("aBool", required: true)]
        public bool ABool { get; set; }

        [Input("aOptional", required: false)]
        public string? AOptional { get; set; }

        [Input("aList", required: true)]
        public List<string> AList { get; set; } = null!;

        [Input("aInputList", required: true)]
        public Input<List<string>> AInputList { get; set; } = null!;

        [Input("aListInput", required: true)]
        public List<Input<string>> AListInput { get; set; } = null!;

        [Input("aDict", required: true)]
        public Dictionary<string, int> ADict { get; set; } = null!;

        [Input("aDictInput", required: true)]
        public Dictionary<string, Input<int>> ADictInput { get; set; } = null!;

        [Input("aInputDict", required: true)]
        public Input<Dictionary<string, int>> AInputDict { get; set; } = null!;

        [Input("aInputDictInput", required: true)]
        public Input<Dictionary<string, Input<int>>> AInputDictInput { get; set; } = null!;

        [Input("aComplexType", required: true)]
        public ComplexTypeArgs AComplexType { get; set; } = null!;

        [Input("aInputComplexType", required: true)]
        public Input<ComplexTypeArgs> AInputComplexType { get; set; } = null!;
    }

    class PlainTypesComponent : ComponentResource
    {
        // Outputs are never plain.

        public PlainTypesComponent(string name, PlainTypesArgs args, ComponentResourceOptions? options = null)
            : base("my-component:index:PlainTypesComponent", name, args, options)
        {
        }
    }

    [Fact]
    public void TestAnalyzeComponentPlainTypes()
    {
        var schema = ComponentAnalyzer.GenerateSchema(_metadata, typeof(PlainTypesComponent));

        var inputs = new Dictionary<string, PropertySpec>();
        // Basic types
        inputs["aInt"] = PropertySpec.CreateBuiltin(BuiltinTypeSpec.Integer, true);
        inputs["aStr"] = PropertySpec.CreateBuiltin(BuiltinTypeSpec.String, true);
        inputs["aFloat"] = PropertySpec.CreateBuiltin(BuiltinTypeSpec.Number, true);
        inputs["aBool"] = PropertySpec.CreateBuiltin(BuiltinTypeSpec.Boolean, true);
        inputs["aOptional"] = PropertySpec.CreateBuiltin(BuiltinTypeSpec.String, true);
        // Lists
        inputs["aList"] = PropertySpec.CreateArray(TypeSpec.CreateBuiltin(BuiltinTypeSpec.String, true));
        inputs["aInputList"] = PropertySpec.CreateArray(TypeSpec.CreateBuiltin(BuiltinTypeSpec.String, true));
        inputs["aListInput"] = PropertySpec.CreateArray(TypeSpec.CreateBuiltin(BuiltinTypeSpec.String));
        // Maps
        inputs["aDict"] = PropertySpec.CreateDictionary(TypeSpec.CreateBuiltin(BuiltinTypeSpec.Integer, true));
        inputs["aDictInput"] = PropertySpec.CreateDictionary(TypeSpec.CreateBuiltin(BuiltinTypeSpec.Integer));
        inputs["aInputDict"] = PropertySpec.CreateDictionary(TypeSpec.CreateBuiltin(BuiltinTypeSpec.Integer, true));
        inputs["aInputDictInput"] = PropertySpec.CreateDictionary(TypeSpec.CreateBuiltin(BuiltinTypeSpec.Integer));
        // Complex types
        inputs["aComplexType"] = PropertySpec.CreateReference("#/types/my-component:index:ComplexType", true);
        inputs["aInputComplexType"] = PropertySpec.CreateReference("#/types/my-component:index:ComplexType");

        var requiredInputs = new HashSet<string> {
            "aInt", "aStr", "aFloat", "aBool", "aList", "aInputList", "aListInput",
            "aDict", "aDictInput", "aInputDict", "aInputDictInput",
            "aComplexType", "aInputComplexType"
        };

        var resources = new Dictionary<string, ResourceSpec>();
        resources.Add("my-component:index:PlainTypesComponent",
            new ResourceSpec(inputs, requiredInputs, new Dictionary<string, PropertySpec>(), new HashSet<string>()));

        var types = new Dictionary<string, ComplexTypeSpec>
        {
            ["my-component:index:ComplexType"] = ComplexTypeSpec.CreateObject(
                new Dictionary<string, PropertySpec>
                {
                    ["aStr"] = PropertySpec.CreateBuiltin(BuiltinTypeSpec.String, true),
                    ["aListStr"] = PropertySpec.CreateArray(TypeSpec.CreateBuiltin(BuiltinTypeSpec.String, true)),
                    ["aDictStr"] = PropertySpec.CreateDictionary(TypeSpec.CreateBuiltin(BuiltinTypeSpec.String, true))
                },
                new HashSet<string> { "aStr" }
            )
        };

        var expected = CreateBasePackageSpec(resources, types);
        AssertSchemaEqual(expected, schema);
    }

    class ListTypesArgs : ResourceArgs
    {
        [Input("requiredList", required: true)]
        public Input<List<string>> RequiredList { get; set; } = null!;

        [Input("optionalList", required: false)]
        public Input<List<string>>? OptionalList { get; set; }

        [Input("array")]
        public Input<string[]>? Array { get; set; }

        [Input("complexList", required: true)]
        public Input<List<ComplexListTypeArgs>> ComplexList { get; set; } = null!;

        [Input("inputList", required: true)]
        public InputList<string> InputList { get; set; } = null!;

        [Input("optionalInputList", required: false)]
        public InputList<string>? OptionalInputList { get; set; }

        [Input("complexInputList", required: true)]
        public InputList<ComplexListTypeArgs> ComplexInputList { get; set; } = null!;
    }

    class ComplexListTypeArgs : ResourceArgs
    {
        [Input("name", required: false)]
        public Input<List<string>>? Name { get; set; }
    }

    class ComplexOutputListType
    {
        [Output("name")]
        public Output<List<string>> Name { get; set; } = null!;
    }

    class ListTypesComponent : ComponentResource
    {
        [Output("simpleList")]
        public Output<List<string>> SimpleList { get; private set; } = null!;

        [Output("complexList")]
        public Output<ComplexOutputListType[]> ComplexList { get; private set; } = null!;

        public ListTypesComponent(string name, ListTypesArgs args, ComponentResourceOptions? options = null)
            : base("my-component:index:ListTypesComponent", name, args, options)
        {
        }
    }

    [Fact]
    public void TestAnalyzeList()
    {
        var schema = ComponentAnalyzer.GenerateSchema(_metadata, typeof(ListTypesComponent));

        var inputs = new Dictionary<string, PropertySpec>
        {
            ["requiredList"] = PropertySpec.CreateArray(TypeSpec.CreateBuiltin(BuiltinTypeSpec.String, true)),
            ["optionalList"] = PropertySpec.CreateArray(TypeSpec.CreateBuiltin(BuiltinTypeSpec.String, true)),
            ["array"] = PropertySpec.CreateArray(TypeSpec.CreateBuiltin(BuiltinTypeSpec.String, true)),
            ["complexList"] = PropertySpec.CreateArray(
                TypeSpec.CreateReference("#/types/my-component:index:ComplexListType", true)
            ),
            ["inputList"] = PropertySpec.CreateArray(TypeSpec.CreateBuiltin(BuiltinTypeSpec.String)),
            ["optionalInputList"] = PropertySpec.CreateArray(TypeSpec.CreateBuiltin(BuiltinTypeSpec.String)),
            ["complexInputList"] = PropertySpec.CreateArray(
                TypeSpec.CreateReference("#/types/my-component:index:ComplexListType")
            )
        };

        var requiredInputs = new HashSet<string> {
            "requiredList", "complexList", "inputList", "complexInputList"
        };

        var outputs = new Dictionary<string, PropertySpec>
        {
            ["simpleList"] = PropertySpec.CreateArray(TypeSpec.CreateBuiltin(BuiltinTypeSpec.String)),
            ["complexList"] = PropertySpec.CreateArray(
                TypeSpec.CreateReference("#/types/my-component:index:ComplexOutputListType")
            )
        };

        var resources = new Dictionary<string, ResourceSpec>();
        resources.Add("my-component:index:ListTypesComponent",
            new ResourceSpec(
                inputs,
                requiredInputs,
                outputs,
                new HashSet<string> { "simpleList", "complexList" }
            ));

        var types = new Dictionary<string, ComplexTypeSpec>
        {
            ["my-component:index:ComplexListType"] = ComplexTypeSpec.CreateObject(
                new Dictionary<string, PropertySpec>
                {
                    ["name"] = PropertySpec.CreateArray(TypeSpec.CreateBuiltin(BuiltinTypeSpec.String, true))
                },
                new HashSet<string>()
            ),
            ["my-component:index:ComplexOutputListType"] = ComplexTypeSpec.CreateObject(
                new Dictionary<string, PropertySpec>
                {
                    ["name"] = PropertySpec.CreateArray(TypeSpec.CreateBuiltin(BuiltinTypeSpec.String))
                },
                new HashSet<string> { "name" }
            )
        };

        var expected = CreateBasePackageSpec(resources, types);
        AssertSchemaEqual(expected, schema);
    }

    class NonStringMapKeyArgs : ResourceArgs
    {
        [Input("badDict", required: true)]
        public Dictionary<int, string> BadDict { get; set; } = null!;
    }

    class NonStringMapKeyComponent : ComponentResource
    {
        public NonStringMapKeyComponent(string name, NonStringMapKeyArgs args, ComponentResourceOptions? options = null)
            : base("my-component:index:NonStringMapKeyComponent", name, args, options)
        {
        }
    }

    [Fact]
    public void TestAnalyzeMapNonStringKey()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ComponentAnalyzer.GenerateSchema(_metadata, typeof(NonStringMapKeyComponent)));

        Assert.Equal(
            "Dictionary keys must be strings, got 'Int32' for 'NonStringMapKeyArgs.BadDict'",
            exception.Message);
    }

    class ComplexDictOutputType
    {
        [Output("name")]
        public Output<Dictionary<string, int>> Name { get; set; } = null!;
    }

    class ComplexDictTypeArgs : ResourceArgs
    {
        [Input("name", required: false)]
        public Input<Dictionary<string, int>>? Name { get; set; }
    }

    class DictTypesArgs : ResourceArgs
    {
        [Input("dictInput", required: true)]
        public Input<Dictionary<string, int>> DictInput { get; set; } = null!;

        [Input("complexDictInput", required: true)]
        public Input<Dictionary<string, ComplexDictTypeArgs>> ComplexDictInput { get; set; } = null!;

        [Input("inputMap", required: true)]
        public InputMap<string> InputMap { get; set; } = null!;

        [Input("complexInputMap", required: true)]
        public InputMap<ComplexDictTypeArgs> ComplexInputMap { get; set; } = null!;
    }

    class DictTypesComponent : ComponentResource
    {
        [Output("dictOutput")]
        public Output<Dictionary<string, int>> DictOutput { get; private set; } = null!;

        [Output("complexDictOutput")]
        public Output<Dictionary<string, ComplexDictOutputType>> ComplexDictOutput { get; private set; } = null!;

        public DictTypesComponent(string name, DictTypesArgs args, ComponentResourceOptions? options = null)
            : base("my-component:index:DictTypesComponent", name, args, options)
        {
        }
    }

    [Fact]
    public void TestAnalyzeDict()
    {
        var schema = ComponentAnalyzer.GenerateSchema(_metadata, typeof(DictTypesComponent));

        var resources = new Dictionary<string, ResourceSpec>();
        resources.Add("my-component:index:DictTypesComponent",
            new ResourceSpec(
                new Dictionary<string, PropertySpec>
                {
                    ["dictInput"] = PropertySpec.CreateDictionary(TypeSpec.CreateBuiltin(BuiltinTypeSpec.Integer, true)),
                    ["complexDictInput"] = PropertySpec.CreateDictionary(
                        TypeSpec.CreateReference("#/types/my-component:index:ComplexDictType", true)),
                    ["inputMap"] = PropertySpec.CreateDictionary(TypeSpec.CreateBuiltin(BuiltinTypeSpec.String)),
                    ["complexInputMap"] = PropertySpec.CreateDictionary(
                        TypeSpec.CreateReference("#/types/my-component:index:ComplexDictType"))
                },
                new HashSet<string> { "dictInput", "complexDictInput", "inputMap", "complexInputMap" },
                new Dictionary<string, PropertySpec>
                {
                    ["dictOutput"] = PropertySpec.CreateDictionary(TypeSpec.CreateBuiltin(BuiltinTypeSpec.Integer)),
                    ["complexDictOutput"] = PropertySpec.CreateDictionary(
                        TypeSpec.CreateReference("#/types/my-component:index:ComplexDictOutputType"))
                },
                new HashSet<string> { "dictOutput", "complexDictOutput" }));

        var types = new Dictionary<string, ComplexTypeSpec>
        {
            ["my-component:index:ComplexDictType"] = ComplexTypeSpec.CreateObject(
                new Dictionary<string, PropertySpec>
                {
                    ["name"] = PropertySpec.CreateDictionary(TypeSpec.CreateBuiltin(BuiltinTypeSpec.Integer, true))
                },
                new HashSet<string>()),
            ["my-component:index:ComplexDictOutputType"] = ComplexTypeSpec.CreateObject(
                new Dictionary<string, PropertySpec>
                {
                    ["name"] = PropertySpec.CreateDictionary(TypeSpec.CreateBuiltin(BuiltinTypeSpec.Integer))
                },
                new HashSet<string> { "name" })
        };

        var expected = CreateBasePackageSpec(resources, types);
        AssertSchemaEqual(expected, schema);
    }

    class ArchiveTypesArgs : ResourceArgs
    {
        [Input("inputArchive", required: true)]
        public Input<Archive> InputArchive { get; set; } = null!;
    }

    class ArchiveTypesComponent : ComponentResource
    {
        [Output("outputArchive")]
        public Output<Archive> OutputArchive { get; private set; } = null!;

        public ArchiveTypesComponent(string name, ArchiveTypesArgs args, ComponentResourceOptions? options = null)
            : base("my-component:index:ArchiveTypesComponent", name, args, options)
        {
        }
    }

    [Fact]
    public void TestAnalyzeArchive()
    {
        var schema = ComponentAnalyzer.GenerateSchema(_metadata, typeof(ArchiveTypesComponent));

        var resources = new Dictionary<string, ResourceSpec>();
        resources.Add("my-component:index:ArchiveTypesComponent",
            new ResourceSpec(
                new Dictionary<string, PropertySpec>
                {
                    ["inputArchive"] = PropertySpec.CreateReference("pulumi.json#/Archive")
                },
                new HashSet<string> { "inputArchive" },
                new Dictionary<string, PropertySpec>
                {
                    ["outputArchive"] = PropertySpec.CreateReference("pulumi.json#/Archive")
                },
                new HashSet<string> { "outputArchive" }));

        var expected = CreateBasePackageSpec(resources);
        AssertSchemaEqual(expected, schema);
    }

    class AssetTypesArgs : ResourceArgs
    {
        [Input("inputAsset", required: true)]
        public Input<Asset> InputAsset { get; set; } = null!;
    }

    class AssetTypesComponent : ComponentResource
    {
        [Output("outputAsset")]
        public Output<Asset> OutputAsset { get; private set; } = null!;

        public AssetTypesComponent(string name, AssetTypesArgs args, ComponentResourceOptions? options = null)
            : base("my-component:index:AssetTypesComponent", name, args, options)
        {
        }
    }

    [Fact]
    public void TestAnalyzeAsset()
    {
        var schema = ComponentAnalyzer.GenerateSchema(_metadata, typeof(AssetTypesComponent));

        var resources = new Dictionary<string, ResourceSpec>();
        resources.Add("my-component:index:AssetTypesComponent",
            new ResourceSpec(
                new Dictionary<string, PropertySpec>
                {
                    ["inputAsset"] = PropertySpec.CreateReference("pulumi.json#/Asset")
                },
                new HashSet<string> { "inputAsset" },
                new Dictionary<string, PropertySpec>
                {
                    ["outputAsset"] = PropertySpec.CreateReference("pulumi.json#/Asset")
                },
                new HashSet<string> { "outputAsset" }));

        var expected = CreateBasePackageSpec(resources);
        AssertSchemaEqual(expected, schema);
    }

    class MyResource : CustomResource
    {
        public MyResource(string name)
            : base("my-component:index:MyResource", name, ResourceArgs.Empty, null)
        {
        }
    }

    class ResourceRefComponentArgs : ResourceArgs
    {
        [Input("resource", required: true)]
        public Input<MyResource> Resource { get; set; } = null!;
    }

    class ResourceRefComponent : ComponentResource
    {
        public ResourceRefComponent(string name, ResourceRefComponentArgs args, ComponentResourceOptions? options = null)
            : base("my-component:index:ResourceRefComponent", name, args, options)
        {
        }
    }

    [Fact]
    public void TestAnalyzeResourceRef()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ComponentAnalyzer.GenerateSchema(_metadata, typeof(ResourceRefComponent)));

        Assert.Equal(
            "Resource references are not supported yet: found type 'MyResource' for 'ResourceRefComponentArgs.Resource'",
            exception.Message);
    }

    class RecursiveTypeArgs : ResourceArgs
    {
        [Input("rec", required: false)]
        public Input<RecursiveTypeArgs>? Rec { get; set; }
    }

    class RecursiveOutputType
    {
        [Output("rec")]
        public Output<RecursiveOutputType> Rec { get; set; } = null!;
    }

    class RecursiveComponentArgs : ResourceArgs
    {
        [Input("rec", required: true)]
        public Input<RecursiveTypeArgs> Rec { get; set; } = null!;
    }

    class RecursiveComponent : ComponentResource
    {
        [Output("rec")]
        public Output<RecursiveOutputType> Rec { get; private set; } = null!;

        public RecursiveComponent(string name, RecursiveComponentArgs args, ComponentResourceOptions? options = null)
            : base("my-component:index:RecursiveComponent", name, args, options)
        {
        }
    }

    [Fact]
    public void TestAnalyzeComponentSelfRecursiveComplexType()
    {
        var schema = ComponentAnalyzer.GenerateSchema(_metadata, typeof(RecursiveComponent));

        var resources = new Dictionary<string, ResourceSpec>();
        resources.Add("my-component:index:RecursiveComponent",
            new ResourceSpec(
                new Dictionary<string, PropertySpec>
                {
                    ["rec"] = PropertySpec.CreateReference("#/types/my-component:index:RecursiveType")
                },
                new HashSet<string> { "rec" },
                new Dictionary<string, PropertySpec>
                {
                    ["rec"] = PropertySpec.CreateReference("#/types/my-component:index:RecursiveOutputType")
                },
                new HashSet<string> { "rec" }));

        var types = new Dictionary<string, ComplexTypeSpec>
        {
            ["my-component:index:RecursiveType"] = ComplexTypeSpec.CreateObject(
                new Dictionary<string, PropertySpec>
                {
                    ["rec"] = PropertySpec.CreateReference("#/types/my-component:index:RecursiveType")
                },
                new HashSet<string>()),
            ["my-component:index:RecursiveOutputType"] = ComplexTypeSpec.CreateObject(
                new Dictionary<string, PropertySpec>
                {
                    ["rec"] = PropertySpec.CreateReference("#/types/my-component:index:RecursiveOutputType")
                },
                new HashSet<string> { "rec" })
        };

        var expected = CreateBasePackageSpec(resources, types);
        AssertSchemaEqual(expected, schema);
    }

    class RecursiveTypeAArgs : ResourceArgs
    {
        [Input("b", required: false)]
        public Input<RecursiveTypeBArgs>? B { get; set; }
    }

    class RecursiveTypeBArgs : ResourceArgs
    {
        [Input("a", required: false)]
        public Input<RecursiveTypeAArgs>? A { get; set; }
    }

    class RecursiveTypeAOutput
    {
        [Output("b")]
        public Output<RecursiveTypeBOutput> B { get; set; } = null!;
    }

    class RecursiveTypeBOutput
    {
        [Output("a")]
        public Output<RecursiveTypeAOutput> A { get; set; } = null!;
    }

    class MutuallyRecursiveComponentArgs : ResourceArgs
    {
        [Input("rec", required: true)]
        public Input<RecursiveTypeAArgs> Rec { get; set; } = null!;
    }

    class MutuallyRecursiveComponent : ComponentResource
    {
        [Output("rec")]
        public Output<RecursiveTypeBOutput> Rec { get; private set; } = null!;

        public MutuallyRecursiveComponent(string name, MutuallyRecursiveComponentArgs args, ComponentResourceOptions? options = null)
            : base("my-component:index:MutuallyRecursiveComponent", name, args, options)
        {
        }
    }

    [Fact]
    public void TestAnalyzeComponentMutuallyRecursiveComplexTypes()
    {
        var schema = ComponentAnalyzer.GenerateSchema(_metadata, typeof(MutuallyRecursiveComponent));

        var resources = new Dictionary<string, ResourceSpec>();
        resources.Add("my-component:index:MutuallyRecursiveComponent",
            new ResourceSpec(
                new Dictionary<string, PropertySpec>
                {
                    ["rec"] = PropertySpec.CreateReference("#/types/my-component:index:RecursiveTypeA")
                },
                new HashSet<string> { "rec" },
                new Dictionary<string, PropertySpec>
                {
                    ["rec"] = PropertySpec.CreateReference("#/types/my-component:index:RecursiveTypeBOutput")
                },
                new HashSet<string> { "rec" }));

        var types = new Dictionary<string, ComplexTypeSpec>
        {
            ["my-component:index:RecursiveTypeA"] = ComplexTypeSpec.CreateObject(
                new Dictionary<string, PropertySpec>
                {
                    ["b"] = PropertySpec.CreateReference("#/types/my-component:index:RecursiveTypeB")
                },
                new HashSet<string>()),
            ["my-component:index:RecursiveTypeB"] = ComplexTypeSpec.CreateObject(
                new Dictionary<string, PropertySpec>
                {
                    ["a"] = PropertySpec.CreateReference("#/types/my-component:index:RecursiveTypeA")
                },
                new HashSet<string>()),
            ["my-component:index:RecursiveTypeAOutput"] = ComplexTypeSpec.CreateObject(
                new Dictionary<string, PropertySpec>
                {
                    ["b"] = PropertySpec.CreateReference("#/types/my-component:index:RecursiveTypeBOutput")
                },
                new HashSet<string> { "b" }),
            ["my-component:index:RecursiveTypeBOutput"] = ComplexTypeSpec.CreateObject(
                new Dictionary<string, PropertySpec>
                {
                    ["a"] = PropertySpec.CreateReference("#/types/my-component:index:RecursiveTypeAOutput")
                },
                new HashSet<string> { "a" })
        };

        var expected = CreateBasePackageSpec(resources, types);
        AssertSchemaEqual(expected, schema);
    }

    class RequiredOptionalTypeArgs : ResourceArgs
    {
        [Input("requiredString", required: true)]
        public Input<string> RequiredString { get; set; } = null!;

        [Input("optionalString")]
        public Input<string>? OptionalString { get; set; }

        [Input("requiredInt", required: true)]
        public Input<int> RequiredInt { get; set; } = null!;

        [Input("optionalInt")]
        public Input<int>? OptionalInt { get; set; }

        [Input("requiredBool", required: true)]
        public Input<bool> RequiredBool { get; set; } = null!;

        [Input("optionalBool")]
        public Input<bool>? OptionalBool { get; set; }
    }

    class RequiredOptionalComponent : ComponentResource
    {
        [Output("requiredOutput")]
        public Output<string> RequiredOutput { get; private set; } = null!;

        [Output("optionalOutput")]
        public Output<string?> OptionalOutput { get; private set; } = null!;

        [Output("requiredIntOutput")]
        public Output<int> RequiredIntOutput { get; private set; } = null!;

        [Output("optionalIntOutput")]
        public Output<int?> OptionalIntOutput { get; private set; } = null!;

        public RequiredOptionalComponent(string name, RequiredOptionalTypeArgs args, ComponentResourceOptions? options = null)
            : base("my-component:index:RequiredOptionalComponent", name, args, options)
        {
        }
    }

    [Fact]
    public void TestAnalyzeRequiredOptionalProperties()
    {
        var schema = ComponentAnalyzer.GenerateSchema(_metadata, typeof(RequiredOptionalComponent));

        var inputs = new Dictionary<string, PropertySpec>
        {
            ["requiredString"] = PropertySpec.String,
            ["optionalString"] = PropertySpec.String,
            ["requiredInt"] = PropertySpec.Integer,
            ["optionalInt"] = PropertySpec.Integer,
            ["requiredBool"] = PropertySpec.Boolean,
            ["optionalBool"] = PropertySpec.Boolean
        };

        var outputs = new Dictionary<string, PropertySpec>
        {
            ["requiredOutput"] = PropertySpec.String,
            ["optionalOutput"] = PropertySpec.String,
            ["requiredIntOutput"] = PropertySpec.Integer,
            ["optionalIntOutput"] = PropertySpec.Integer
        };

        var resources = new Dictionary<string, ResourceSpec>();
        resources.Add("my-component:index:RequiredOptionalComponent",
            new ResourceSpec(
                inputs,
                new HashSet<string> { "requiredString", "requiredInt", "requiredBool" },
                outputs,
                new HashSet<string> { "requiredOutput", "requiredIntOutput" }));

        var expected = CreateBasePackageSpec(resources);
        AssertSchemaEqual(expected, schema);
    }

    [Theory]
    [InlineData("123test", "Package name must start with a letter and contain only letters, numbers, hyphens, and underscores")]
    [InlineData("test!", "Package name must start with a letter and contain only letters, numbers, hyphens, and underscores")]
    [InlineData("test space", "Package name must start with a letter and contain only letters, numbers, hyphens, and underscores")]
    [InlineData("", "Package name cannot be empty or whitespace")]
    [InlineData(" ", "Package name cannot be empty or whitespace")]
    [InlineData(null, "Package name cannot be empty or whitespace")]
    public void TestInvalidPackageNames(string? name, string expectedError)
    {
        var metadata = new Metadata(name!);
        var exception = Assert.Throws<ArgumentException>(() =>
            ComponentAnalyzer.GenerateSchema(metadata, typeof(SelfSignedCertificate)));

        Assert.Equal($"{expectedError} (Parameter 'metadata')", exception.Message);
    }

    [Theory]
    [InlineData("test")]
    [InlineData("test-package")]
    [InlineData("test_package")]
    [InlineData("test123")]
    [InlineData("myPackage")]
    public void TestValidPackageNames(string name)
    {
        var metadata = new Metadata(name);
        var schema = ComponentAnalyzer.GenerateSchema(metadata, typeof(SelfSignedCertificate));
        Assert.Equal(name, schema.Name);
    }

    private readonly Metadata _metadata = new Metadata("my-component", "0.0.1", "Test package");

    private static PackageSpec CreateBasePackageSpec(
        Dictionary<string, ResourceSpec>? resources = null,
        Dictionary<string, ComplexTypeSpec>? types = null)
    {
        var pkg = new PackageSpec
        {
            Name = "my-component",
            Version = "0.0.1",
            DisplayName = "Test package"
        };

        var languageSettings = new Dictionary<string, object>
        {
            { "respectSchemaVersion", true }
        }.ToImmutableSortedDictionary();

        var languages = new Dictionary<string, ImmutableSortedDictionary<string, object>>();
        foreach (var lang in new[] { "nodejs", "python", "csharp", "java", "go" })
        {
            languages[lang] = languageSettings;
        }

        return pkg with
        {
            Language = ImmutableSortedDictionary.CreateRange(languages),
            Resources = resources?.ToImmutableSortedDictionary() ?? ImmutableSortedDictionary<string, ResourceSpec>.Empty,
            Types = types?.ToImmutableSortedDictionary() ?? ImmutableSortedDictionary<string, ComplexTypeSpec>.Empty
        };
    }

    private static void AssertSchemaEqual(PackageSpec expected, PackageSpec actual)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var expectedJson = JsonSerializer.Serialize(expected, options);
        var actualJson = JsonSerializer.Serialize(actual, options);
        Assert.Equal(expectedJson, actualJson);
    }
}

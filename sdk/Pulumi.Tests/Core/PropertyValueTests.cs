namespace Pulumi.Tests.Cores;

using Pulumi.Tests.Serialization;
using Pulumi.Experimental;
using Pulumi.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

public class PropertyValueTests
{
#pragma warning disable CS0618
    PropertyValueSerializer CreateSerializer() => new();
#pragma warning restore CS0618

    PropertyValue Object(params KeyValuePair<string, PropertyValue>[] pairs)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, PropertyValue>();
        foreach (var pair in pairs)
        {
            builder.Add(pair.Key, pair.Value);
        }

        return new PropertyValue(builder.ToImmutable());
    }

    KeyValuePair<string, PropertyValue> Pair(string key, PropertyValue value) => new(key, value);
    PropertyValue Array(params PropertyValue[] values) => new(values.ToImmutableArray());

    class BasicArgs : ResourceArgs
    {
        public int PasswordLength { get; set; }
    }

    [Fact]
    public async Task DeserializingBasicArgsWorks()
    {
        var serializer = CreateSerializer();
        var data = Object(
            Pair("PasswordLength", new PropertyValue(10)));

        var basicArgs = await serializer.Deserialize<BasicArgs>(data);
        Assert.Equal(10, basicArgs.PasswordLength);
    }

    class SecretArgs : ResourceArgs
    {
        [Input("password", required: true)]
        private Input<string>? _password;
        public Input<string>? Password
        {
            get => _password;
            set
            {
                var emptySecret = Output.CreateSecret(0);
                _password = Output.Tuple<Input<string>?, int>(value, emptySecret).Apply(t => t.Item1);
            }
        }
    }

    [Fact]
    public async Task DeserializingSecretArgsWorks()
    {
        var serializer = CreateSerializer();
        var data = Object(
            Pair("password", new PropertyValue("PW").WithSecret(true)));

        var basicArgs = await serializer.Deserialize<SecretArgs>(data);
        var passwordOutput = await basicArgs.Password.ToOutput().DataTask;
        Assert.True(passwordOutput.IsSecret);
        Assert.True(passwordOutput.IsKnown);
        Assert.Equal("PW", passwordOutput.Value);
    }

    class UsingNullableArgs : ResourceArgs
    {
        public int? Length { get; set; }
    }

    [Fact]
    public async Task DeserializingNullableArgsWorks()
    {
        var serializer = CreateSerializer();
        var emptyData = Object();
        var withoutData = await serializer.Deserialize<UsingNullableArgs>(emptyData);
        Assert.False(withoutData.Length.HasValue, "Nullable value is null");

        var data = Object(Pair("Length", new PropertyValue(10)));
        var withData = await serializer.Deserialize<UsingNullableArgs>(data);
        Assert.True(withData.Length.HasValue, "Nullable field has a value");
        Assert.Equal(10, withData.Length);
    }

    class UsingListArgs : ResourceArgs
    {
        public string[] First { get; set; } = default!;
        public List<string> Second { get; set; } = default!;
        public ImmutableArray<string> Third { get; set; } = default!;

        [Input("firstWithField")]
        private string[]? _firstWithField;
        public string[] FirstWithField
        {
            get => _firstWithField ??= System.Array.Empty<string>();
            set => _firstWithField = value;
        }

        [Input("secondWithField")]
        private List<string>? _secondWithField;
        public List<string> SecondWithField
        {
            get => _secondWithField ??= new List<string>();
            set => _secondWithField = value;
        }

        [Input("thirdWithField")]
        private ImmutableArray<string>? _thirdWithField;
        public ImmutableArray<string> ThirdWithField
        {
            get => _thirdWithField ??= new ImmutableArray<string>();
            set => _thirdWithField = value;
        }
    }

    [Fact]
    public async Task DeserializingListTypesWorks()
    {
        var serializer = CreateSerializer();
        var array = Array(
            new PropertyValue("one"),
            new PropertyValue("two"),
            new PropertyValue("three"));

        var data = Object(
            Pair("First", array),
            Pair("Second", array),
            Pair("Third", array),
            Pair("firstWithField", array),
            Pair("secondWithField", array),
            Pair("thirdWithField", array));

        var args = await serializer.Deserialize<UsingListArgs>(data);

        var elements = new string[] { "one", "two", "three" };
        Assert.Equal(elements, args.First);
        Assert.Equal(elements, args.Second.ToArray());
        Assert.Equal(elements, args.Third.ToArray());
        Assert.Equal(elements, args.FirstWithField);
        Assert.Equal(elements, args.SecondWithField.ToArray());
        Assert.Equal(elements, args.ThirdWithField.ToArray());
    }

    class StringFromNullBecomesEmpty : ResourceArgs
    {
        public string Data { get; set; } = string.Empty;
    }

    [Fact]
    public async Task DeserializingStringFromNullValueWorks()
    {
        var serializer = CreateSerializer();
        var data = Object(Pair("Data", PropertyValue.Null));
        var argsWithNullString = await serializer.Deserialize<StringFromNullBecomesEmpty>(data);
        Assert.True(String.IsNullOrEmpty(argsWithNullString.Data));
    }

    class UsingDictionaryArgs : ResourceArgs
    {
        public Dictionary<string, string> First { get; set; } = default!;
        public ImmutableDictionary<string, string> Second { get; set; } = default!;

        [Input("firstWithField")]
        private Dictionary<string, string>? _firstWithField;

        public Dictionary<string, string> FirstWithField
        {
            get => _firstWithField ??= new Dictionary<string, string>();
            set => _firstWithField = value;
        }

        [Input("secondWithField")]
        private ImmutableDictionary<string, string>? _secondWithField;
        public ImmutableDictionary<string, string> SecondWithField
        {
            get => _secondWithField ??= ImmutableDictionary.Create<string, string>();
            set => _secondWithField = value;
        }
    }

    [Fact]
    public async Task DeserializingDictionaryPropertiesWork()
    {
        var serializer = CreateSerializer();
        var simpleDictionary = Object(Pair("Uno", new PropertyValue("One")));
        var data = Object(
            Pair("First", simpleDictionary),
            Pair("Second", simpleDictionary),
            Pair("firstWithField", simpleDictionary),
            Pair("secondWithField", simpleDictionary));

        var args = await serializer.Deserialize<UsingDictionaryArgs>(data);

        var expected = new Dictionary<string, string>
        {
            ["Uno"] = "One"
        };

        Assert.Equal(expected, args.First);
        Assert.Equal(expected, args.Second.ToDictionary(x => x.Key, y => y.Value));
        Assert.Equal(expected, args.FirstWithField);
        Assert.Equal(expected, args.SecondWithField.ToDictionary(x => x.Key, y => y.Value));

        var emptyObject = Object();
        var emptyArgs = await serializer.Deserialize<UsingDictionaryArgs>(emptyObject);
        Assert.Null(emptyArgs.First);
        Assert.Null(emptyArgs.Second);
        Assert.Empty(emptyArgs.FirstWithField);
        Assert.Empty(emptyArgs.SecondWithField);
    }

    class UsingInputArgs : ResourceArgs
    {
        public Input<string> Name { get; set; } = default!;
        public InputList<string> Subnets { get; set; } = default!;
        public InputMap<string> Tags { get; set; } = default!;

        [Input("subnetsWithField", required: true)]
        private InputList<string>? _subnetsWithField;

        public InputList<string> SubnetsWithField
        {
            get => _subnetsWithField ??= new InputList<string>();
            set => _subnetsWithField = value;
        }

        [Input("tagsWithField", required: true)]
        private InputMap<string>? _tagsWithField;

        public InputMap<string> TagsWithField
        {
            get => _tagsWithField ??= new InputMap<string>();
            set => _tagsWithField = value;
        }
    }

    [Fact]
    public async Task DeserializingInputTypesWorks()
    {
        var serializer = CreateSerializer();
        var data = Object(
            Pair("Name", new PropertyValue("test")),
            Pair("Subnets", Array(
                new PropertyValue("one"),
                new PropertyValue("two"),
                new PropertyValue("three"))),
            Pair("Tags", Object(
                Pair("one", new PropertyValue("one")),
                Pair("two", new PropertyValue("two")),
                Pair("three", new PropertyValue("three")))),
            Pair("subnetsWithField", Array(
                new PropertyValue("one"),
                new PropertyValue("two"),
                new PropertyValue("three"))),
            Pair("tagsWithField", Object(
                Pair("one", new PropertyValue("one")),
                Pair("two", new PropertyValue("two")),
                Pair("three", new PropertyValue("three"))))
        );

        var args = await serializer.Deserialize<UsingInputArgs>(data);

        var name = await args.Name.ToOutput().GetValueAsync("");
        Assert.Equal("test", name);

        var subnets = await args.Subnets.ToOutput().GetValueAsync(ImmutableArray<string>.Empty);
        Assert.Equal(new[] { "one", "two", "three" }, subnets.ToArray());

        var tags = await args.Tags.ToOutput().GetValueAsync(ImmutableDictionary<string, string>.Empty);
        Assert.Equal(new Dictionary<string, string>
        {
            ["one"] = "one",
            ["two"] = "two",
            ["three"] = "three"
        }, tags);

        var subnetsWithField = await args.SubnetsWithField.ToOutput().GetValueAsync(ImmutableArray<string>.Empty);
        Assert.Equal(new[] { "one", "two", "three" }, subnetsWithField.ToArray());

        var tagsWithField = await args.TagsWithField.ToOutput().GetValueAsync(ImmutableDictionary<string, string>.Empty);
        Assert.Equal(new Dictionary<string, string>
        {
            ["one"] = "one",
            ["two"] = "two",
            ["three"] = "three"
        }, tagsWithField);
    }

    class RequireIntArgs : ResourceArgs
    {
        public int Property { get; set; }
    }

    [Fact]
    public async Task DeserializingEmptyValuesIntoRequiredPropertyShouldFail()
    {
        try
        {
            var serializer = CreateSerializer();
            var emptyObject = Object();
            await serializer.Deserialize<RequireIntArgs>(emptyObject);
        }
        catch (Exception e)
        {
            var errorMessage =
                "Error while deserializing value of type Int32 from property value of type Null. Expected Number instead at path [$, Property].";
            Assert.Contains(errorMessage, e.Message);
        }
    }

    class OptionalIntInputArgs : ResourceArgs
    {
        public Input<int?> OptionalInteger { get; set; } = default!;
    }

    [Fact]
    public async Task DeserializingOptionalInputWorks()
    {
        var serializer = CreateSerializer();
        var emptyData = Object();
        var args = await serializer.Deserialize<OptionalIntInputArgs>(emptyData);
        var optionalInteger = await args.OptionalInteger.ToOutput().GetValueAsync(0);
        Assert.False(optionalInteger.HasValue);
    }

    enum TestEnum { Allow, Default }
    class EnumArgs : ResourceArgs
    {
        public TestEnum EnumInput { get; set; }
    }

    [Fact]
    public async Task DeserializingEnumWorks()
    {
        var serializer = CreateSerializer();
        var data = Object(Pair("EnumInput", new PropertyValue(1)));
        var args = await serializer.Deserialize<EnumArgs>(data);
        Assert.Equal(TestEnum.Default, args.EnumInput);
    }

    class EnumTypeStringArgs : ResourceArgs
    {
        [Input(nameof(ContainerColor))]
        public ComplexTypeConverterTests.ContainerColor ContainerColor { get; set; }
    }

    [Fact]
    public async Task SerializingEnumTypeStringWorks()
    {
        var serializer = CreateSerializer();
        var containerColor = ComplexTypeConverterTests.ContainerColor.Blue;
        var serialized = await serializer.Serialize(new EnumTypeStringArgs
        {
            ContainerColor = containerColor
        });
        var expected = Object(Pair(nameof(EnumTypeStringArgs.ContainerColor), new PropertyValue((string)containerColor)));
        Assert.Equal(expected, serialized);
    }

    [Fact]
    public async Task DeserializingEnumTypeFromStringWorks()
    {
        var serializer = CreateSerializer();
        var containerColor = ComplexTypeConverterTests.ContainerColor.Blue;
        var data = Object(Pair(nameof(EnumTypeStringArgs.ContainerColor), new PropertyValue((string)containerColor)));
        var args = await serializer.Deserialize<EnumTypeStringArgs>(data);
        Assert.Equal(containerColor, args.ContainerColor);
    }

    class EnumTypeDoubleArgs : ResourceArgs
    {
        [Input(nameof(ContainerBrightness))]
        public EnumConverterTests.ContainerBrightness ContainerBrightness { get; set; }
    }

    [Fact]
    public async Task SerializingEnumTypeDoubleWorks()
    {
        var serializer = CreateSerializer();
        var containerBrightness = EnumConverterTests.ContainerBrightness.ZeroPointOne;
        var serialized = await serializer.Serialize(new EnumTypeDoubleArgs
        {
            ContainerBrightness = containerBrightness
        });
        var expected = Object(Pair(nameof(EnumTypeDoubleArgs.ContainerBrightness), new PropertyValue((double)containerBrightness))); ;
        Assert.Equal(expected, serialized);
    }

    [Fact]
    public async Task DeserializingEnumTypeFromDoubleWorks()
    {
        var serializer = CreateSerializer();
        var containerBrightness = EnumConverterTests.ContainerBrightness.ZeroPointOne;
        var data = Object(Pair(nameof(EnumTypeDoubleArgs.ContainerBrightness), new PropertyValue((double)containerBrightness)));
        var args = await serializer.Deserialize<EnumTypeDoubleArgs>(data);
        Assert.Equal(containerBrightness, args.ContainerBrightness);
    }

    [Fact]
    public async Task DeserializingUnknownInputsWorks()
    {
        var serializer = CreateSerializer();
        var deserialized = await serializer.Deserialize<Input<string>>(PropertyValue.Computed);
        var output = deserialized.ToOutput();
        var data = await output.DataTask;
        Assert.Null(data.Value);
        Assert.False(data.IsSecret);
        Assert.False(data.IsKnown);
    }

    [Fact]
    public async Task DeserializingSecretUnknownInputsWorks()
    {
        var serializer = CreateSerializer();
        var deserialized = await serializer.Deserialize<Input<string>>(PropertyValue.Computed.WithSecret(true));
        var output = deserialized.ToOutput();
        var data = await output.DataTask;
        Assert.Null(data.Value);
        Assert.True(data.IsSecret);
        Assert.False(data.IsKnown);
    }

    [Fact]
    public async Task DeserializingSecretInputsWorks()
    {
        var serializer = CreateSerializer();
        var secretValue = new PropertyValue("Hello").WithSecret(true);
        var deserialized = await serializer.Deserialize<Input<string>>(secretValue);
        var output = deserialized.ToOutput();
        var data = await output.DataTask;
        Assert.Equal("Hello", data.Value);
        Assert.True(data.IsSecret);
        Assert.True(data.IsKnown);
    }

    [Fact]
    public async Task DeserializingBasicInputsWorks()
    {
        var serializer = CreateSerializer();
        var resources = ImmutableHashSet.Create(new Urn("pulumi:pulumi:Stack"));
        var output = new PropertyValue("Hello").WithDependencies(resources);

        var deserialized = await serializer.Deserialize<Input<string>>(output);
        var deserializedOutput = deserialized.ToOutput();
        var data = await deserializedOutput.DataTask;
        Assert.Equal("Hello", data.Value);
        Assert.False(data.IsSecret);
        Assert.True(data.IsKnown);
        Assert.Single(data.Resources);
        if (data.Resources.First() is DependencyResource dependencyResource)
        {
            var resolvedUrn = await dependencyResource.Urn.GetValueAsync("");
            Assert.Equal("pulumi:pulumi:Stack", resolvedUrn);
        }
        else
        {
            throw new Exception("Expected deserialized resource to be a dependency resource");
        }
    }

    [Fact]
    public async Task DeserializingUnknownOutputWorks()
    {
        var serializer = CreateSerializer();
        var resources = ImmutableHashSet.Create(new Urn("pulumi:pulumi:Stack"));
        var output = PropertyValue.Computed.WithDependencies(resources);

        var deserialized = await serializer.Deserialize<Input<string>>(output);
        var deserializedOutput = deserialized.ToOutput();
        var data = await deserializedOutput.DataTask;
        Assert.Null(data.Value);
        Assert.False(data.IsSecret);
        Assert.False(data.IsKnown);
    }

    [Fact]
    public async Task DeserializingWrappedOutputSecretWorks()
    {
        var serializer = CreateSerializer();
        var resources = ImmutableHashSet.Create(new Urn("pulumi:pulumi:Stack"));
        var secretOutput = new PropertyValue("Hello").WithSecret(true).WithDependencies(resources);

        var deserialized = await serializer.Deserialize<Input<string>>(secretOutput);
        var deserializedOutput = deserialized.ToOutput();
        var data = await deserializedOutput.DataTask;
        Assert.Equal("Hello", data.Value);
        Assert.True(data.IsSecret);
        Assert.True(data.IsKnown);
    }

    [Fact]
    public async Task DeserializingWrappedUnknownOutputSecretWorks()
    {
        var serializer = CreateSerializer();
        var resources = ImmutableHashSet.Create(new Urn("pulumi:pulumi:Stack"));
        var secretOutput = PropertyValue.Computed.WithSecret(true).WithDependencies(resources);

        var deserialized = await serializer.Deserialize<Input<string>>(secretOutput);
        var deserializedOutput = deserialized.ToOutput();
        var data = await deserializedOutput.DataTask;
        Assert.Null(data.Value);
        Assert.True(data.IsSecret);
        Assert.False(data.IsKnown);
    }

    [Fact]
    public async Task DeserializingWrappedSecretInOutputWorks()
    {
        var serializer = CreateSerializer();
        // secretOutput = Output(Secret("Hello"))
        var resources = ImmutableHashSet.Create(new Urn("pulumi:pulumi:Stack"));
        var secretOutput = new PropertyValue("Hello").WithSecret(true).WithDependencies(resources);

        var deserialized = await serializer.Deserialize<Input<string>>(secretOutput);
        var deserializedOutput = deserialized.ToOutput();
        var data = await deserializedOutput.DataTask;
        Assert.Equal("Hello", data.Value);
        Assert.True(data.IsSecret);
        Assert.True(data.IsKnown);
    }


    [Fact]
    public async Task DeserializingWrappedUnknownSecretInOutputWorks()
    {
        var serializer = CreateSerializer();
        var resources = ImmutableHashSet.Create(new Urn("pulumi:pulumi:Stack"));
        var secretOutput = PropertyValue.Computed.WithSecret(true).WithDependencies(resources);

        var deserialized = await serializer.Deserialize<Input<string>>(secretOutput);
        var deserializedOutput = deserialized.ToOutput();
        var data = await deserializedOutput.DataTask;
        Assert.Null(data.Value);
        Assert.True(data.IsSecret);
        Assert.False(data.IsKnown);
    }

    [Fact]
    public async Task SerializingPrimtivesWorks()
    {
        var serializer = CreateSerializer();
        Assert.Equal(new PropertyValue("Hello"), await serializer.Serialize("Hello"));
        Assert.Equal(new PropertyValue(10), await serializer.Serialize(10));
        Assert.Equal(new PropertyValue(10.5), await serializer.Serialize(10.5));
        Assert.Equal(new PropertyValue(true), await serializer.Serialize(true));
        Assert.Equal(new PropertyValue(false), await serializer.Serialize(false));
        Assert.Equal(PropertyValue.Null, await serializer.Serialize<object?>(null));
    }

    [Fact]
    public async Task SerializingOutputsWorks()
    {
        var serializer = CreateSerializer();

        var plainOutput = Output.Create("Hello");
        var serialized = await serializer.Serialize(plainOutput);
        var expected = new PropertyValue("Hello");
        Assert.Equal(expected, serialized);

        var plainUnknown = OutputUtilities.CreateUnknown("");
        serialized = await serializer.Serialize(plainUnknown);
        expected = PropertyValue.Computed;
        Assert.Equal(expected, serialized);

        var secretUnknown = Output.CreateSecret(OutputUtilities.CreateUnknown(""));
        serialized = await serializer.Serialize(secretUnknown);
        expected = PropertyValue.Computed.WithSecret(true);
        Assert.Equal(expected, serialized);

        var resource = new DependencyResource("urn:pulumi::stack::proj::type::name");
        var unknownWithDeps = OutputUtilities.WithDependency(OutputUtilities.CreateUnknown(""), resource);
        serialized = await serializer.Serialize(unknownWithDeps);
        expected = PropertyValue.Computed.WithDependencies(
            [new Urn("urn:pulumi::stack::proj::type::name")]);
        Assert.Equal(expected, serialized);
    }

    [Fact]
    public async Task SerializingInputsWork()
    {
        var serializer = CreateSerializer();

        var input = (Input<string>)"Hello";
        var serialized = await serializer.Serialize(input);
        var expected = new PropertyValue("Hello");
        Assert.Equal(expected, serialized);
    }

    [Fact]
    public async Task SerializingPropertyValueReturnsItself()
    {
        var serializer = CreateSerializer();
        var value = new PropertyValue("Hello");
        Assert.Equal(value, await serializer.Serialize(value));
    }

    [Fact]
    public async Task SerializingCollectionsWorks()
    {
        var serializer = CreateSerializer();

        var expected = Array(
            new PropertyValue("one"),
            new PropertyValue("two"),
            new PropertyValue("three"));

        // array
        Assert.Equal(expected, await serializer.Serialize(new[] { "one", "two", "three" }));

        // list
        Assert.Equal(expected, await serializer.Serialize(new List<string> { "one", "two", "three" }));

        // immutable array
        Assert.Equal(expected, await serializer.Serialize(ImmutableArray.Create("one", "two", "three")));

        IEnumerable<string> Seq()
        {
            yield return "one";
            yield return "two";
            yield return "three";
        }

        // generic sequences
        Assert.Equal(expected, await serializer.Serialize(Seq()));

        // default immutable array serializes to empty array
        // a special case since default(ImmutableArray<T>) is not null but cannot be enumerated directly
        Assert.Equal(Array(), await serializer.Serialize(default(ImmutableArray<string>)));

        // sets
        Assert.Equal(expected, await serializer.Serialize(new HashSet<string> { "one", "two", "three" }));

        // dictionaries
        var expectedDict = Object(
            Pair("one", new PropertyValue("one")),
            Pair("two", new PropertyValue("two")),
            Pair("three", new PropertyValue("three")));

        Assert.Equal(expectedDict, await serializer.Serialize(new Dictionary<string, string>
        {
            ["one"] = "one",
            ["two"] = "two",
            ["three"] = "three"
        }));

        // immutable dictionaries
        Assert.Equal(expectedDict, await serializer.Serialize(ImmutableDictionary.CreateRange(new Dictionary<string, string>
        {
            ["one"] = "one",
            ["two"] = "two",
            ["three"] = "three"
        })));
    }

    [Fact]
    public async Task SerializingEnumWorks()
    {
        var serializer = CreateSerializer();
        Assert.Equal(new PropertyValue(0), await serializer.Serialize(TestEnum.Allow));
        Assert.Equal(new PropertyValue(1), await serializer.Serialize(TestEnum.Default));
    }

    class CustomArgs
    {
        [Input("value_field")]
        public string Value { get; set; } = string.Empty;

        [Output("name_field")]
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public async Task SerializingClassWherePropertiesHaveOverriddenNamesWorks()
    {
        var serializer = CreateSerializer();
        var args = new CustomArgs
        {
            Value = "value",
            Name = "name"
        };

        var expected = Object(
            Pair("value_field", new PropertyValue("value")),
            Pair("name_field", new PropertyValue("name")));

        Assert.Equal(expected, await serializer.Serialize(args));
    }

    [Fact]
    public async Task DeserializingClassWherePropertiesHaveOverriddenNamesWorks()
    {
        var serializer = CreateSerializer();
        var data = Object(
            Pair("value_field", new PropertyValue("value")),
            Pair("name_field", new PropertyValue("name")));

        var args = await serializer.Deserialize<CustomArgs>(data);
        Assert.Equal("value", args.Value);
        Assert.Equal("name", args.Name);
    }

    class TypeWithConstructors
    {
        public int First { get; set; }
        public int? Second { get; set; }

        public TypeWithConstructors()
        {
        }

        public TypeWithConstructors(int first)
        {
            First = first * 10;
            Second = first;
        }

        public TypeWithConstructors(int first, int? second)
        {
            First = first;
            Second = second;
        }
    }

    class TypeWithOptionalConstructorParameter
    {
        public int First { get; set; }
        public int? Second { get; set; }

        public TypeWithOptionalConstructorParameter(int first)
        {
            First = first * 5;
            Second = first;
        }
    }

    [Fact]
    public async Task DeserializingClassWithMultipleConstructorsWorks()
    {
        var serializer = CreateSerializer();
        var dataFirstConstructor = Object(
            Pair("first", new PropertyValue(1)));

        // should choose the constructor matching (int first)
        var firstConstructor = await serializer.Deserialize<TypeWithConstructors>(dataFirstConstructor);
        Assert.Equal(10, firstConstructor.First);
        Assert.Equal(1, firstConstructor.Second);

        // should choose the constructor matching (int first, int? second)
        var dataSecondConstructor = Object(
            Pair("first", new PropertyValue(1)),
            Pair("second", new PropertyValue(2)));

        var secondConstructor = await serializer.Deserialize<TypeWithConstructors>(dataSecondConstructor);
        Assert.Equal(1, secondConstructor.First);
        Assert.Equal(2, secondConstructor.Second);

        // should choose the default parameterless constructor
        // then follow deserialization logic by matching property names
        var objectShapeData = Object(
            Pair("First", new PropertyValue(42)),
            Pair("Second", new PropertyValue(420)));

        var objectShape = await serializer.Deserialize<TypeWithConstructors>(objectShapeData);
        Assert.Equal(42, objectShape.First);
        Assert.Equal(420, objectShape.Second);

        var typeWithOptionalParameterCtor = await serializer.Deserialize<TypeWithOptionalConstructorParameter>(
            Object(Pair("first", new PropertyValue(1))));

        Assert.Equal(5, typeWithOptionalParameterCtor.First);
        Assert.Equal(1, typeWithOptionalParameterCtor.Second);
    }

    [OutputType]
    public class OuterOutputType
    {
        [Output("nestedOutput")]
        public required Output<NestedOutputType> NestedOutput { get; init; }
    }

    [OutputType]
    public class NestedOutputType
    {
        [Output("stringOutput")]
        public required Output<string> StringOutput { get; init; }
    }

    [Fact]
    public async Task SerializingNestedOutputWorks()
    {
        var serializer = CreateSerializer();

        var serialized = await serializer.Serialize(new OuterOutputType()
        {
            NestedOutput = Output.Create(new NestedOutputType
            {
                StringOutput = Output.Create("hello")
            })
        });

        var expected = Object(
            Pair("nestedOutput", Object(Pair("stringOutput", new PropertyValue("hello")))));

        Assert.Equal(expected, serialized);
    }

    [Fact]
    public async Task DeerializingNestedOutputWorks()
    {
        var serializer = CreateSerializer();
        var value = Object(
            Pair("nestedOutput", Object(Pair("stringOutput", new PropertyValue("hello")))));

        var serialized = await serializer.Deserialize<OuterOutputType>(value);

        var nestedValueOutput = await serialized.NestedOutput.DataTask;
        var stringValueOutput = await nestedValueOutput.Value.StringOutput.DataTask;
        Assert.Equal("hello", stringValueOutput.Value);
    }

    public class TagsType : ResourceArgs
    {
        [Input("tags", required: false)] private InputMap<string>? _tags;

        public InputMap<string> Tags
        {
            get => _tags ??= [];
            set => _tags = value;
        }
    }

    [Fact]
    public async Task DeserializingUnknownOutputInputMapWorks()
    {
        var serializer = CreateSerializer();

        var resource = new DependencyResource("urn:pulumi::stack::proj::type::name");
        var unknownWithDeps = OutputUtilities.WithDependency(OutputUtilities.CreateUnknown(""), resource);
        var serialized = await serializer.Serialize(unknownWithDeps);

        var value = Object(Pair("tags", Object(Pair("staticString", new PropertyValue("string")),
            Pair("staticString2", serialized))));

        var result = await serializer.Deserialize<TagsType>(value);

        var tags = await result.Tags.ToOutput().DataTask;
        Assert.False(tags.IsKnown, "tags should be unknown");
    }
}

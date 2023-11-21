namespace Pulumi.Tests.Provider;

using Pulumi.Experimental.Provider;
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
        Assert.Equal(10, withData.Length.Value);
    }

    class UsingListArgs : ResourceArgs
    {
        public string[] First { get; set; }
        public List<string> Second { get; set; }
        public ImmutableArray<string> Third { get; set; }
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
            Pair("Third", array));

        var args = await serializer.Deserialize<UsingListArgs>(data);

        var elements = new string[] { "one", "two", "three" };
        Assert.Equal(elements, args.First);
        Assert.Equal(elements, args.Second.ToArray());
        Assert.Equal(elements, args.Third.ToArray());
    }

    class StringFromNullBecomesEmpty : ResourceArgs
    {
        public string Data { get; set; }
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
        public Dictionary<string, string> First { get; set; }
        public ImmutableDictionary<string, string> Second { get; set; }
    }

    [Fact]
    public async Task DeserializingDictionaryPropertiesWork()
    {
        var serializer = CreateSerializer();
        var simpleDictionary = Object(Pair("Uno", new PropertyValue("One")));
        var data = Object(
            Pair("First", simpleDictionary),
            Pair("Second", simpleDictionary));

        var args = await serializer.Deserialize<UsingDictionaryArgs>(data);

        var expected = new Dictionary<string, string>
        {
            ["Uno"] = "One"
        };

        Assert.Equal(expected, args.First);
        Assert.Equal(expected, args.Second.ToDictionary(x => x.Key, y => y.Value));

        var emptyObject = Object();
        var emptyArgs = await serializer.Deserialize<UsingDictionaryArgs>(emptyObject);
        Assert.Null(emptyArgs.First);
        Assert.Null(emptyArgs.Second);
    }

    class UsingInputArgs : ResourceArgs
    {
        public Input<string> Name { get; set; }
        public InputList<string> Subnets { get; set; }
        public InputMap<string> Tags { get; set; }
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
        public Input<int?> OptionalInteger { get; set; }
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

    [Fact]
    public async Task DeserializingUnknownInputsWorks()
    {
        var serializer = CreateSerializer();
        var x = Output.Create("");
        var deserialized = await serializer.Deserialize<Input<string>>(PropertyValue.Computed);
        var output = deserialized.ToOutput();
        var known = await OutputUtilities.GetIsKnownAsync(output);
        Assert.False(known);
    }

    [Fact]
    public async Task DeserializingSecretInputsWorks()
    {
        var serializer = CreateSerializer();
        var secretValue = new PropertyValue(new PropertyValue("Hello"));
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
        var resources = ImmutableArray.Create("pulumi:pulumi:Stack");
        var output = new PropertyValue(new OutputReference(
            value: new PropertyValue("Hello"),
            dependencies: resources));

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
    public async Task DeserializingWrappedOutputSecretWorks()
    {
        var serializer = CreateSerializer();
        // secretOutput = Secret(Output("Hello"))
        var secretOutput = new PropertyValue(
            new PropertyValue(new OutputReference(
                value: new PropertyValue("Hello"),
                dependencies: ImmutableArray<string>.Empty)));

        var deserialized = await serializer.Deserialize<Input<string>>(secretOutput);
        var deserializedOutput = deserialized.ToOutput();
        var data = await deserializedOutput.DataTask;
        Assert.Equal("Hello", data.Value);
        Assert.True(data.IsSecret);
        Assert.True(data.IsKnown);
    }

    [Fact]
    public async Task DeserializingWrappedSecretInOutputWorks()
    {
        var serializer = CreateSerializer();
        // secretOutput = Output(Secret("Hello"))
        var secretOutput = new PropertyValue(
            new OutputReference(
                value: new PropertyValue(new PropertyValue("Hello")),
                dependencies: ImmutableArray<string>.Empty));

        var deserialized = await serializer.Deserialize<Input<string>>(secretOutput);
        var deserializedOutput = deserialized.ToOutput();
        var data = await deserializedOutput.DataTask;
        Assert.Equal("Hello", data.Value);
        Assert.True(data.IsSecret);
        Assert.True(data.IsKnown);
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
        public string Value { get; set; }
        [Output("name_field")]
        public string Name { get; set; }
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
}

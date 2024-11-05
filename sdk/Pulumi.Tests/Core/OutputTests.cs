// Copyright 2016-2019, Pulumi Corporation

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Pulumi.Serialization;
using Pulumi.Utilities;
using Xunit;

namespace Pulumi.Tests.Core
{
    // Simple struct used for JSON tests
    public struct TestStructure
    {
        public Input<int> X { get; set; }

        private Output<int> y;

        public Output<string> Z => y.Apply(y => (y + 1).ToString());

        public int W { get; set; }

        public TestStructure(Input<int> x, Output<int> y, int w)
        {
            X = x;
            this.y = y;
            W = w;
        }
    }

    // Struct with nested input lists used for JSON tests
    public struct TestListStructure
    {
        public InputList<Item> Items { get; set; }

        public TestListStructure(InputList<Item> items)
        {
            Items = items;
        }

        public class Item
        {
            public InputList<int> Ints { get; set; }

            public InputMap<int> IntMap { get; set; }

            public Item(InputList<int> ints, InputMap<int> intMap)
            {
                Ints = ints;
                IntMap = intMap;
            }
        }
    }

    public class OutputTests : PulumiTest
    {
        private static Output<T> CreateOutput<T>(T value, bool isKnown, bool isSecret = false)
            => new Output<T>(Task.FromResult(OutputData.Create(
                ImmutableHashSet<Resource>.Empty, value, isKnown, isSecret)));

        private static Output<T> CreateOutput<T>(IEnumerable<Resource> resources, T value, bool isKnown, bool isSecret = false)
            => new Output<T>(Task.FromResult(OutputData.Create(
                ImmutableHashSet.CreateRange(resources), value, isKnown, isSecret)));

        public class PreviewTests
        {
            [Fact]
            public Task ApplyCanRunOnKnownValue()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true);
                    var o2 = o1.Apply(a => a + 1);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.Equal(1, data.Value);
                });

            [Fact]
            public Task ApplyCanRunOnKnownAwaitableValue()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true);
                    var o2 = o1.Apply(a => Task.FromResult("inner"));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.Equal("inner", data.Value);
                });

            [Fact]
            public Task ApplyCanRunOnKnownKnownOutputValue()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: true));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.Equal("inner", data.Value);
                });

            [Fact]
            public Task ApplyCanRunOnKnownUnknownOutputValue()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: false));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.Equal("inner", data.Value);
                });

            [Fact]
            public Task ApplyProducesUnknownDefaultOnUnknown()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: false);
                    var o2 = o1.Apply(a => a + 1);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.Equal(0, data.Value);
                });

            [Fact]
            public Task ApplyProducesUnknownDefaultOnUnknownAwaitable()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: false);
                    var o2 = o1.Apply(a => Task.FromResult("inner"));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.Null(data.Value);
                });

            [Fact]
            public Task ApplyProducesUnknownDefaultOnUnknownKnownOutput()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: false);
                    var o2 = o1.Apply(a => CreateOutput("", isKnown: true));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.Null(data.Value);
                });

            [Fact]
            public Task ApplyProducesUnknownDefaultOnUnknownUnknownOutput()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: false);
                    var o2 = o1.Apply(a => CreateOutput("", isKnown: false));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.Null(data.Value);
                });

            [Fact]
            public Task ApplyPreservesSecretOnKnown()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true, isSecret: true);
                    var o2 = o1.Apply(a => a + 1);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.True(data.IsSecret);
                    Assert.Equal(1, data.Value);
                });

            [Fact]
            public Task ApplyPreservesSecretOnKnownAwaitable()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true, isSecret: true);
                    var o2 = o1.Apply(a => Task.FromResult("inner"));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.True(data.IsSecret);
                    Assert.Equal("inner", data.Value);
                });

            [Fact]
            public Task ApplyPreservesSecretOnKnownKnownOutput()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true, isSecret: true);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: true));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.True(data.IsSecret);
                    Assert.Equal("inner", data.Value);
                });

            [Fact]
            public Task ApplyPreservesSecretOnKnownUnknownOutput()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true, isSecret: true);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: false));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.True(data.IsSecret);
                    Assert.Equal("inner", data.Value);
                });

            [Fact]
            public Task ApplyPreservesSecretOnUnknown()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: false, isSecret: true);
                    var o2 = o1.Apply(a => a + 1);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.True(data.IsSecret);
                    Assert.Equal(0, data.Value);
                });

            [Fact]
            public Task ApplyPreservesSecretOnUnknownAwaitable()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: false, isSecret: true);
                    var o2 = o1.Apply(a => Task.FromResult("inner"));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.True(data.IsSecret);
                    Assert.Null(data.Value);
                });

            [Fact]
            public Task ApplyPreservesSecretOnUnknownKnownOutput()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: false, isSecret: true);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: true));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.True(data.IsSecret);
                    Assert.Null(data.Value);
                });

            [Fact]
            public Task ApplyPreservesSecretOnUnknownUnknownOutput()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: false, isSecret: true);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: false));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.True(data.IsSecret);
                    Assert.Null(data.Value);
                });

            [Fact]
            public Task ApplyPropagatesSecretOnKnownKnownOutput()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: true, isSecret: true));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.True(data.IsSecret);
                    Assert.Equal("inner", data.Value);
                });

            [Fact]
            public Task ApplyPropagatesSecretOnKnownUnknownOutput()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: false, isSecret: true));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.True(data.IsSecret);
                    Assert.Equal("inner", data.Value);
                });

            [Fact]
            public Task ApplyDoesNotPropagateSecretOnUnknownKnownOutput()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: false);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: true, isSecret: true));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.False(data.IsSecret);
                    Assert.Null(data.Value);
                });

            [Fact]
            public Task ApplyDoesNotPropagateSecretOnUnknownUnknownOutput()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: false);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: false, isSecret: true));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.False(data.IsSecret);
                    Assert.Null(data.Value);
                });

            [Fact]
            public Task AllParamsOutputs()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(1, isKnown: true);
                    var o2 = CreateOutput(2, isKnown: true);
                    var o3 = Output.All(o1, o2);
                    var data = await o3.DataTask.ConfigureAwait(false);
                    Assert.Equal(new[] { 1, 2 }, data.Value);
                });

            [Fact]
            public Task AllEnumerableOutputs()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(1, isKnown: true);
                    var o2 = CreateOutput(2, isKnown: true);
                    var outputs = new[] { o1, o2 }.AsEnumerable();
                    var o3 = Output.All(outputs);
                    var data = await o3.DataTask.ConfigureAwait(false);
                    Assert.Equal(new[] { 1, 2 }, data.Value);
                });

            [Fact]
            public Task AllParamsInputs()
                => RunInPreview(async () =>
                {
                    var i1 = (Input<int>)CreateOutput(1, isKnown: true);
                    var i2 = (Input<int>)CreateOutput(2, isKnown: true);
                    var o = Output.All(i1, i2);
                    var data = await o.DataTask.ConfigureAwait(false);
                    Assert.Equal(new[] { 1, 2 }, data.Value);
                });

            [Fact]
            public Task AllEnumerableInputs()
                => RunInPreview(async () =>
                {
                    var i1 = (Input<int>)CreateOutput(1, isKnown: true);
                    var i2 = (Input<int>)CreateOutput(2, isKnown: true);
                    var inputs = new[] { i1, i2 }.AsEnumerable();
                    var o = Output.All(inputs);
                    var data = await o.DataTask.ConfigureAwait(false);
                    Assert.Equal(new[] { 1, 2 }, data.Value);
                });

            [Fact]
            public Task IsSecretAsyncOnKnownOutput()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true, isSecret: true);
                    var o2 = CreateOutput(1, isKnown: true, isSecret: false);
                    var isSecret1 = await Output.IsSecretAsync(o1).ConfigureAwait(false);
                    var isSecret2 = await Output.IsSecretAsync(o2).ConfigureAwait(false);
                    Assert.True(isSecret1);
                    Assert.False(isSecret2);
                });

            [Fact]
            public Task IsSecretAsyncOnAwaitableOutput()
                => RunInPreview(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true, isSecret: true).Apply(a => Task.FromResult("inner1"));
                    var o2 = CreateOutput(1, isKnown: true, isSecret: false).Apply(a => Task.FromResult("inner2"));
                    var isSecret1 = await Output.IsSecretAsync(o1).ConfigureAwait(false);
                    var isSecret2 = await Output.IsSecretAsync(o2).ConfigureAwait(false);
                    Assert.True(isSecret1);
                    Assert.False(isSecret2);
                });

            [Fact]
            public Task UnsecretOnKnownSecretValue()
                => RunInPreview(async () =>
                {
                    var secret = CreateOutput(1, isKnown: true, isSecret: true);
                    var notSecret = Output.Unsecret(secret);
                    var notSecretData = await notSecret.DataTask.ConfigureAwait(false);
                    Assert.False(notSecretData.IsSecret);
                    Assert.Equal(1, notSecretData.Value);
                });

            [Fact]
            public Task UnsecretOnAwaitableSecretValue()
                => RunInPreview(async () =>
                {
                    var secret = CreateOutput(0, isKnown: true, isSecret: true).Apply(a => Task.FromResult("inner"));
                    var notSecret = Output.Unsecret(secret);
                    var notSecretData = await notSecret.DataTask.ConfigureAwait(false);
                    Assert.False(notSecretData.IsSecret);
                    Assert.Equal("inner", notSecretData.Value);
                });

            [Fact]
            public Task UnsecretOnNonSecretValue()
                => RunInPreview(async () =>
                {
                    var secret = CreateOutput(2, isKnown: true, isSecret: false);
                    var notSecret = Output.Unsecret(secret);
                    var notSecretData = await notSecret.DataTask.ConfigureAwait(false);
                    Assert.False(notSecretData.IsSecret);
                    Assert.Equal(2, notSecretData.Value);
                });

            [Fact]
            public Task CreateUnknownSkipsValueFactory()
                => RunInPreview(async () =>
                {
                    var output = OutputUtilities.CreateUnknown(() => Task.FromResult("value"));
                    var data = await output.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.Null(data.Value);
                });
        }

        public class NormalTests
        {
            [Fact]
            public Task ApplyCanRunOnKnownValue()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true);
                    var o2 = o1.Apply(a => a + 1);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.Equal(1, data.Value);
                });

            [Fact]
            public Task ApplyCanRunOnKnownAwaitableValue()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true);
                    var o2 = o1.Apply(a => Task.FromResult("inner"));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.Equal("inner", data.Value);
                });

            [Fact]
            public Task ApplyCanRunOnKnownKnownOutputValue()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: true));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.Equal("inner", data.Value);
                });

            [Fact]
            public Task ApplyCanRunOnKnownUnknownOutputValue()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: false));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.Equal("inner", data.Value);
                });

            [Fact]
            public Task ApplyProducesKnownOnUnknown()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: false);
                    var o2 = o1.Apply(a => a + 1);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                });

            [Fact]
            public Task ApplyProducesKnownOnUnknownAwaitable()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: false);
                    var o2 = o1.Apply(a => Task.FromResult("inner"));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                });

            [Fact]
            public Task ApplyProducesKnownOnUnknownKnownOutput()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: false);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: true));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                });

            [Fact]
            public Task ApplyProducesUnknownOnUnknownUnknownOutput()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: false);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: false));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                });

            [Fact]
            public Task ApplyPreservesSecretOnKnown()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true, isSecret: true);
                    var o2 = o1.Apply(a => a + 1);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.True(data.IsSecret);
                    Assert.Equal(1, data.Value);
                });

            [Fact]
            public Task ApplyPreservesSecretOnKnownAwaitable()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true, isSecret: true);
                    var o2 = o1.Apply(a => Task.FromResult("inner"));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.True(data.IsSecret);
                    Assert.Equal("inner", data.Value);
                });

            [Fact]
            public Task ApplyPreservesSecretOnKnownKnownOutput()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true, isSecret: true);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: true));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.True(data.IsSecret);
                    Assert.Equal("inner", data.Value);
                });

            [Fact]
            public Task ApplyPreservesSecretOnKnownUnknownOutput()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true, isSecret: true);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: false));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.True(data.IsSecret);
                    Assert.Equal("inner", data.Value);
                });

            [Fact]
            public Task ApplyPreservesSecretOnUnknown()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: false, isSecret: true);
                    var o2 = o1.Apply(a => a + 1);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.True(data.IsSecret);
                });

            [Fact]
            public Task ApplyPreservesSecretOnUnknownAwaitable()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: false, isSecret: true);
                    var o2 = o1.Apply(a => Task.FromResult("inner"));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.True(data.IsSecret);
                });

            [Fact]
            public Task ApplyPreservesSecretOnUnknownKnownOutput()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: false, isSecret: true);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: true));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.True(data.IsSecret);
                });

            [Fact]
            public Task ApplyPreservesSecretOnUnknownUnknownOutput()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: false, isSecret: true);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: false));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.True(data.IsSecret);
                });

            [Fact]
            public Task ApplyPropagatesSecretOnKnownKnownOutput()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: true, isSecret: true));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.True(data.IsSecret);
                    Assert.Equal("inner", data.Value);
                });

            [Fact]
            public Task ApplyPropagatesSecretOnKnownUnknownOutput()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, isKnown: true);
                    var o2 = o1.Apply(a => CreateOutput("inner", isKnown: false, isSecret: true));
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.True(data.IsSecret);
                    Assert.Equal("inner", data.Value);
                });

            [Fact]
            public Task CreateUnknownRunsValueFactory()
                => RunInNormal(async () =>
                {
                    var output = OutputUtilities.CreateUnknown(() => Task.FromResult("value"));
                    var data = await output.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                });

            [Fact]
            public Task CreateSecretSetsSecret()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(0, true);
                    var o2 = Output.CreateSecret(o1);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.True(data.IsSecret);
                    Assert.Equal(0, data.Value);
                });

            [Fact]
            public Task JsonSerializeBasic()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(new int[] { 0, 1 }, true);
                    var o2 = Output.JsonSerialize(o1);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.False(data.IsSecret);
                    Assert.Equal("[0,1]", data.Value);
                });

            [Fact]
            public Task JsonSerializeNested()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(new Output<int>[] {
                        CreateOutput(0, true),
                        CreateOutput(1, true),
                    }, true);
                    var o2 = Output.JsonSerialize(o1);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.False(data.IsSecret);
                    Assert.Equal("[0,1]", data.Value);
                });

            [Fact]
            public Task JsonSerializeNestedUnknown()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(new Output<int>[] {
                        CreateOutput<int>(default, false),
                        CreateOutput(1, true),
                    }, true);
                    var o2 = Output.JsonSerialize(o1);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.False(data.IsKnown);
                    Assert.False(data.IsSecret);
                });

            [Fact]
            public Task JsonSerializeNestedSecret()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput(new Output<int>[] {
                        CreateOutput(0, true, true),
                        CreateOutput(1, true),
                    }, true);
                    var o2 = Output.JsonSerialize(o1);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.True(data.IsSecret);
                    Assert.Equal("[0,1]", data.Value);
                });

            [Fact]
            public Task JsonSerializeNestedCollections()
                => RunInNormal(async () =>
                {
                    var listv = new InputList<int> { 1, 2, 3 };
                    var mapv = new InputMap<int> { { "K1", 4 } };
                    var v = new TestListStructure(
                        new InputList<TestListStructure.Item> { new TestListStructure.Item(listv, mapv) }
                    );
                    var o1 = CreateOutput(v, true);
                    var o2 = Output.JsonSerialize(o1);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.False(data.IsSecret);
                    var expected = "{\"Items\":[{\"Ints\":[1,2,3],\"IntMap\":{\"K1\":4}}]}";
                    Assert.Equal(expected, data.Value);
                });

            [Fact]
            public Task JsonSerializeWithOptions()
                => RunInNormal(async () =>
                {
                    var v = new System.Collections.Generic.Dictionary<string, TestStructure>();
                    v.Add("a", new TestStructure(1, Output.Create(2), 3));
                    v.Add("b", new TestStructure(int.MinValue, Output.Create(int.MaxValue), 0));
                    var o1 = CreateOutput(v, true);
                    var options = new System.Text.Json.JsonSerializerOptions();
                    options.WriteIndented = true;
                    var o2 = Output.JsonSerialize(o1, options);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.False(data.IsSecret);
                    var expected = @"{
  ""a"": {
    ""X"": 1,
    ""Z"": ""3"",
    ""W"": 3
  },
  ""b"": {
    ""X"": -2147483648,
    ""Z"": ""-2147483648"",
    ""W"": 0
  }
}";
                    // we need to normalize line endings to make this work cross platform
                    Assert.Equal(expected, data.Value.ReplaceLineEndings("\n"));
                });

            [Fact]
            public async Task JsonSerializeNestedDependencies()
            {
                // We need a custom mock setup for this because new CustomResource will call into the
                // deployment to try and register.
                var runner = new Moq.Mock<IRunner>(Moq.MockBehavior.Strict);
                runner.Setup(r => r.RegisterTask(Moq.It.IsAny<string>(), Moq.It.IsAny<Task>()));

                var logger = new Moq.Mock<IEngineLogger>(Moq.MockBehavior.Strict);
                logger.Setup(l => l.DebugAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<Resource>(), Moq.It.IsAny<int?>(), Moq.It.IsAny<bool?>())).Returns(Task.CompletedTask);

                var mock = new Moq.Mock<IDeploymentInternal>(Moq.MockBehavior.Strict);
                mock.Setup(d => d.IsDryRun).Returns(false);
                mock.Setup(d => d.Stack).Returns(() => null!);
                mock.Setup(d => d.Runner).Returns(runner.Object);
                mock.Setup(d => d.Logger).Returns(logger.Object);
                mock.Setup(d => d.ReadOrRegisterResource(
                   Moq.It.IsAny<Resource>(),
                   Moq.It.IsAny<bool>(),
                   Moq.It.IsAny<System.Func<string, Resource>>(),
                   Moq.It.IsAny<ResourceArgs>(),
                   Moq.It.IsAny<ResourceOptions>(),
                   Moq.It.IsAny<RegisterPackageRequest?>()));

                Deployment.Instance = new DeploymentInstance(mock.Object);

                var resource = new CustomResource("type", "name", ResourceArgs.Empty, new CustomResourceOptions());

                var o1 = CreateOutput(new Output<int>[] {
                    CreateOutput(new Resource[] { resource}, 0, true, true),
                    CreateOutput(1, true),
                }, true);
                var o2 = Output.JsonSerialize(o1);
                var data = await o2.DataTask.ConfigureAwait(false);
                Assert.True(data.IsKnown);
                Assert.True(data.IsSecret);
                Assert.Contains(resource, data.Resources);
                Assert.Equal("[0,1]", data.Value);
            }

            [Fact]
            public Task JsonDeserializeBasic()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput("[0,1]", true);
                    var o2 = Output.JsonDeserialize<int[]>(o1);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.False(data.IsSecret);
                    Assert.Equal(new int[] { 0, 1 }, data.Value);
                });

            [Fact]
            public Task JsonDeserializeNested()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput("[0,1]", true);
                    var o2 = Output.JsonDeserialize<Output<int>[]>(o1);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.False(data.IsSecret);
                    var i0 = await data.Value[0].DataTask;
                    Assert.True(i0.IsKnown);
                    Assert.False(i0.IsSecret);
                    Assert.Equal(0, i0.Value);
                    var i1 = await data.Value[1].DataTask;
                    Assert.True(i1.IsKnown);
                    Assert.False(i1.IsSecret);
                    Assert.Equal(1, i1.Value);
                });

            [Fact]
            public Task JsonDeserializeNestedSecret()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput("[0,1]", true, true);
                    var o2 = Output.JsonDeserialize<Output<int>[]>(o1);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    Assert.True(data.IsSecret);
                    var i0 = await data.Value[0].DataTask;
                    Assert.True(i0.IsKnown);
                    Assert.True(i0.IsSecret);
                    Assert.Equal(0, i0.Value);
                    var i1 = await data.Value[1].DataTask;
                    Assert.True(i1.IsKnown);
                    Assert.True(i1.IsSecret);
                    Assert.Equal(1, i1.Value);
                });

            [Fact]
            public Task JsonDeserializeNestedCollections()
                => RunInNormal(async () =>
                {
                    var o1 = CreateOutput("{\"Items\":[{\"Ints\":[1,2,3],\"IntMap\":{\"K1\":4}}]}", true);
                    var o2 = Output.JsonDeserialize<Output<TestListStructure>>(o1);
                    var data = await o2.DataTask.ConfigureAwait(false);
                    Assert.True(data.IsKnown);
                    var i0 = await data.Value.DataTask.ConfigureAwait(false);
                    var items = await i0.Value.Items.ToOutput().DataTask.ConfigureAwait(false);
                    Assert.Single(items.Value);
                    var listv = await items.Value[0].Ints.ToOutput().DataTask.ConfigureAwait(false);
                    Assert.Equal(3, listv.Value.Length);
                    for (var i = 1; i <= 3; i++)
                    {
                        Assert.Equal(i, listv.Value[i - 1]);
                    }
                    var mapv = await items.Value[0].IntMap.ToOutput().DataTask.ConfigureAwait(false);
                    Assert.Single(mapv.Value);
                    Assert.Equal(4, mapv.Value["K1"]);
                });

            [Fact]
            public async Task JsonDeserializeNestedDependencies()
            {
                // We need a custom mock setup for this because new CustomResource will call into the
                // deployment to try and register.
                var runner = new Moq.Mock<IRunner>(Moq.MockBehavior.Strict);
                runner.Setup(r => r.RegisterTask(Moq.It.IsAny<string>(), Moq.It.IsAny<Task>()));

                var logger = new Moq.Mock<IEngineLogger>(Moq.MockBehavior.Strict);
                logger.Setup(l => l.DebugAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<Resource>(), Moq.It.IsAny<int?>(), Moq.It.IsAny<bool?>())).Returns(Task.CompletedTask);

                var mock = new Moq.Mock<IDeploymentInternal>(Moq.MockBehavior.Strict);
                mock.Setup(d => d.IsDryRun).Returns(false);
                mock.Setup(d => d.Stack).Returns(() => null!);
                mock.Setup(d => d.Runner).Returns(runner.Object);
                mock.Setup(d => d.Logger).Returns(logger.Object);
                mock.Setup(d => d.ReadOrRegisterResource(
                    Moq.It.IsAny<Resource>(),
                    Moq.It.IsAny<bool>(),
                    Moq.It.IsAny<System.Func<string, Resource>>(),
                    Moq.It.IsAny<ResourceArgs>(),
                    Moq.It.IsAny<ResourceOptions>(),
                    Moq.It.IsAny<RegisterPackageRequest?>()));

                Deployment.Instance = new DeploymentInstance(mock.Object);

                var resource = new CustomResource("type", "name", ResourceArgs.Empty, new CustomResourceOptions());

                var o1 = CreateOutput(new Resource[] { resource }, "[0,1]", true, true);
                var o2 = Output.JsonDeserialize<Output<int>[]>(o1);
                var data = await o2.DataTask.ConfigureAwait(false);
                Assert.True(data.IsKnown);
                Assert.True(data.IsSecret);
                Assert.Contains(resource, data.Resources);
                var i0 = await data.Value[0].DataTask;
                Assert.True(i0.IsKnown);
                Assert.True(i0.IsSecret);
                Assert.Equal(0, i0.Value);
                Assert.Contains(resource, i0.Resources);
                var i1 = await data.Value[1].DataTask;
                Assert.True(i1.IsKnown);
                Assert.True(i1.IsSecret);
                Assert.Equal(1, i1.Value);
                Assert.True(data.IsKnown);
                Assert.True(data.IsSecret);
                Assert.Contains(resource, i1.Resources);
            }

            [Fact]
            public Task FormatBasic() => RunInNormal(async () =>
            {
                var o1 = CreateOutput(0, true);
                var o2 = Output.Format($"{o1}");
                var data = await o2.DataTask.ConfigureAwait(false);
                Assert.True(data.IsKnown);
                Assert.False(data.IsSecret);
                Assert.Equal("0", data.Value);
            });

            [Fact]
            public Task FormatBraceSyntax() => RunInNormal(async () =>
            {
                var o2 = Output.Format($"{{ pip pip");
                var data = await o2.DataTask.ConfigureAwait(false);
                Assert.True(data.IsKnown);
                Assert.False(data.IsSecret);
                Assert.Equal("{ pip pip", data.Value);
            });
        }
    }
}

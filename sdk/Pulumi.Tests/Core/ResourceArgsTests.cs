// Copyright 2016-2019, Pulumi Corporation

using System.Collections.Immutable;
using System.Threading.Tasks;
using Xunit;

namespace Pulumi.Tests.Core
{
    public class ResourceArgsTests : PulumiTest
    {
        #region ComplexResourceArgs1

        public class ComplexResourceArgs1 : ResourceArgs
        {
            [Input("s")] public Input<string> S { get; set; } = null!;
            [Input("array")] private InputList<bool> _array = null!;
            public InputList<bool> Array
            {
                // ReSharper disable once ConstantNullCoalescingCondition
                get => _array ??= new InputList<bool>();
                set => _array = value;
            }
        }

        [Fact]
        public async Task TestComplexResourceArgs1_NullValues()
        {
            var args = new ComplexResourceArgs1();
            var dictionary = await args.ToDictionaryAsync();

            Assert.True(dictionary.TryGetValue("s", out var sValue));
            Assert.True(dictionary.TryGetValue("array", out var arrayValue));

            Assert.Null(sValue);
            Assert.Null(arrayValue);
        }

        [Fact]
        public async Task TestComplexResourceArgs1_SetField()
        {
            var args = new ComplexResourceArgs1
            {
                S = "val",
            };

            var dictionary = await args.ToDictionaryAsync();

            Assert.True(dictionary.TryGetValue("s", out var sValue));
            Assert.True(dictionary.TryGetValue("array", out var arrayValue));

            Assert.NotNull(sValue);
            Assert.Null(arrayValue);

            var output = ((IInput)sValue!).ToOutput();
            var data = await output.GetDataAsync();
            Assert.Equal("val", data.Value);
        }

        [Fact]
        public Task TestComplexResourceArgs1_SetProperty()
        {
            return RunInNormal(async () =>
            {
                var args = new ComplexResourceArgs1
                {
                    Array = { true },
                };

                var dictionary = await args.ToDictionaryAsync();

                Assert.True(dictionary.TryGetValue("s", out var sValue));
                Assert.True(dictionary.TryGetValue("array", out var arrayValue));

                Assert.Null(sValue);
                Assert.NotNull(arrayValue);

                var output = ((IInput)arrayValue!).ToOutput();
                var data = await output.GetDataAsync();
                AssertEx.SequenceEqual(
                    ImmutableArray<bool>.Empty.Add(true), (ImmutableArray<bool>)data.Value!);
            });
        }

        #endregion

        #region JsonResourceArgs1

        public class JsonResourceArgs1 : ResourceArgs
        {
            [Input("array", json: true)] private InputList<bool> _array = null!;
            public InputList<bool> Array
            {
                // ReSharper disable once ConstantNullCoalescingCondition
                get => _array ??= new InputList<bool>();
                set => _array = value;
            }

            [Input("map", json: true)] private InputMap<int> _map = null!;
            public InputMap<int> Map
            {
                // ReSharper disable once ConstantNullCoalescingCondition
                get => _map ??= new InputMap<int>();
                set => _map = value;
            }
        }

        static async Task<Pulumi.Serialization.OutputData<object?>> GetData(object value)
        {
            if (value is IInput input)
            {
                var output = input.ToOutput();
                return await output.GetDataAsync();
            }
            return new Pulumi.Serialization.OutputData<object?>(ImmutableHashSet<Resource>.Empty, value, false, false);
        }

        [Fact]
        public async Task TestJsonMap()
        {
            var args = new JsonResourceArgs1
            {
                Array = { true, false },
                Map =
                {
                    { "k1", 1 },
                    { "k2", 2 },
                },
            };
            var dictionary = await args.ToDictionaryAsync();

            Assert.True(dictionary.TryGetValue("array", out var arrayValue));
            Assert.True(dictionary.TryGetValue("map", out var mapValue));

            Assert.NotNull(arrayValue);
            Assert.NotNull(mapValue);

            var arrayData = await GetData(arrayValue!);
            var mapData = await GetData(mapValue!);

            Assert.Equal("[ true, false ]", arrayData.Value);
            Assert.Equal("{ \"k1\": 1, \"k2\": 2 }", mapData.Value);
        }

        [Fact]
        public async Task TestJsonMapUnknown()
        {
            await PulumiTest.RunInPreview(async () =>
            {
                var unknownB = new Output<bool>(Task.FromResult(Pulumi.Serialization.OutputData.Create(
                    ImmutableHashSet<Resource>.Empty, false, false, false)));

                var unknownI = new Output<int>(Task.FromResult(Pulumi.Serialization.OutputData.Create(
                    ImmutableHashSet<Resource>.Empty, 0, false, false)));

                var args = new JsonResourceArgs1
                {
                    Array = { unknownB, false },
                    Map =
                    {
                        { "k1", unknownI },
                        { "k2", 2 },
                    },
                };

                var dictionary = await args.ToDictionaryAsync();

                Assert.True(dictionary.TryGetValue("array", out var arrayValue));
                Assert.True(dictionary.TryGetValue("map", out var mapValue));

                Assert.NotNull(arrayValue);
                Assert.NotNull(mapValue);

                var arrayData = await GetData(arrayValue!);
                var mapData = await GetData(mapValue!);

                Assert.False(arrayData.IsKnown);
                Assert.False(mapData.IsKnown);
            });
        }

        [Fact]
        public async Task TestJsonMapSecret()
        {
            var args = new JsonResourceArgs1
            {
                Array = { Output.CreateSecret(true), false },
                Map =
                {
                    { "k1", Output.CreateSecret(1) },
                    { "k2", 2 },
                },
            };
            var dictionary = await args.ToDictionaryAsync();

            Assert.True(dictionary.TryGetValue("array", out var arrayValue));
            Assert.True(dictionary.TryGetValue("map", out var mapValue));

            Assert.NotNull(arrayValue);
            Assert.NotNull(mapValue);

            var arrayData = await GetData(arrayValue!);
            var mapData = await GetData(mapValue!);

            Assert.True(arrayData.IsSecret);
            Assert.Equal("[ true, false ]", arrayData.Value);

            Assert.True(mapData.IsSecret);
            Assert.Equal("{ \"k1\": 1, \"k2\": 2 }", mapData.Value);
        }

        #endregion
    }
}

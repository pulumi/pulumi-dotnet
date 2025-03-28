// Copyright 2016-2019, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Xunit;

namespace Pulumi.Tests.Core
{
    public class InputTests : PulumiTest
    {
        [Fact]
        public Task MergeInputMaps()
            => RunInPreview(async () =>
            {
                var map1 = new InputMap<string>
                {
                    { "K1", "V1" },
                    { "K2", Output.Create("V2") },
                    { "K3", Output.Create("V3_wrong") }
                };

                var map2 = new InputMap<string>
                {
                    { "K3", Output.Create("V3") },
                    { "K4", "V4" }
                };

                var result = InputMap<string>.Merge(map1, map2);

                // Check the merged map
                var data = await result.ToOutput().DataTask;
                Assert.True(data.IsKnown);
                Assert.Equal(4, data.Value.Count);
                for (var i = 1; i <= 4; i++)
                    Assert.True(data.Value.Contains($"K{i}", $"V{i}"));

                // Check that the input maps haven't changed
                var map1Data = await map1.ToOutput().DataTask;
                Assert.Equal(3, map1Data.Value.Count);
                Assert.True(map1Data.Value.ContainsValue("V3_wrong"));

                var map2Data = await map2.ToOutput().DataTask;
                Assert.Equal(2, map2Data.Value.Count);
                Assert.True(map2Data.Value.ContainsValue("V3"));
            });

        [Fact]
        public Task InputMapCollectionInitializers()
            => RunInPreview(async () =>
            {
                var map = new InputMap<string>
                {
                    { "K1", "V1" },
                    { "K2", Output.Create("V2") },
                    new Dictionary<string, string> { { "K3", "V3" }, { "K4", "V4"} },
                    Output.Create(new Dictionary<string, string> { ["K5"] = "V5", ["K6"] = "V6" }.ToImmutableDictionary())
                };
                var data = await map.ToOutput().DataTask;
                Assert.Equal(6, data.Value.Count);
                Assert.Equal(new Dictionary<string, string> { ["K1"] = "V1", ["K2"] = "V2", ["K3"] = "V3", ["K4"] = "V4", ["K5"] = "V5", ["K6"] = "V6" }, data.Value);
            });

        [Fact]
        public Task InputMapUnionInitializer()
            => RunInPreview(async () =>
            {
                var sample = new SampleArgs
                {
                    Dict =
                    {
                        { "left", "testValue" },
                        { "right", 123 },
                        { "t0", Union<string, int>.FromT0("left") },
                        { "t1", Union<string, int>.FromT1(456) },
                    }
                };
                var data = await sample.Dict.ToOutput().DataTask;
                Assert.Equal(4, data.Value.Count);
                Assert.True(data.Value.ContainsValue("testValue"));
                Assert.True(data.Value.ContainsValue(123));
                Assert.True(data.Value.ContainsValue("left"));
                Assert.True(data.Value.ContainsValue(456));
            });

        [Fact]
        public Task InputListCollectionInitializers()
            => RunInPreview(async () =>
            {
                var list = new InputList<string>
                {
                    "V1",
                    Output.Create("V2"),
                    new[] { "V3", "V4" },
                    new List<string> { "V5", "V6" },
                    Output.Create(ImmutableArray.Create("V7", "V8"))
                };
                var data = await list.ToOutput().DataTask;
                Assert.Equal(8, data.Value.Length);
                Assert.Equal(new[] { "V1", "V2", "V3", "V4", "V5", "V6", "V7", "V8" }, data.Value);
            });

        [Fact]
        public Task InputListUnionInitializer()
            => RunInPreview(async () =>
            {
                var sample = new SampleArgs
                {
                    List =
                    {
                        "testValue",
                        123,
                        Union<string, int>.FromT0("left"),
                        Union<string, int>.FromT1(456),
                    }
                };
                var data = await sample.List.ToOutput().DataTask;
                Assert.Equal(4, data.Value.Length);
                Assert.True(data.Value.IndexOf("testValue") >= 0);
                Assert.True(data.Value.IndexOf(123) >= 0);
                Assert.True(data.Value.IndexOf("left") >= 0);
                Assert.True(data.Value.IndexOf(456) >= 0);
            });

        [Fact]
        public Task InputUnionInitializer()
            => RunInPreview(async () =>
            {
                var sample = new SampleArgs { Union = "testValue" };
                var data = await sample.Union.ToOutput().DataTask;
                Assert.Equal("testValue", data.Value);

                sample = new SampleArgs { Union = 123 };
                data = await sample.Union.ToOutput().DataTask;
                Assert.Equal(123, data.Value);

                sample = new SampleArgs { Union = Union<string, int>.FromT0("left") };
                data = await sample.Union.ToOutput().DataTask;
                Assert.Equal("left", data.Value);

                sample = new SampleArgs { Union = Union<string, int>.FromT1(456) };
                data = await sample.Union.ToOutput().DataTask;
                Assert.Equal(456, data.Value);
            });

        [Fact]
        public Task InputMapAdd()
            => RunInPreview(async () =>
            {
                var map = new InputMap<string>();

                // Add a key and value
                map.Add("K1", "V1");
                // Add the a new key and the same value twice
                map.Add("K2", "V2");
                map.Add("K2", "V2");

                // We should be able to get this map still
                var data = await map.ToOutput().DataTask;
                Assert.Equal(2, data.Value.Count);
                Assert.Equal(new Dictionary<string, string> { ["K1"] = "V1", ["K2"] = "V2" }, data.Value);

                // Add a new key and two different values
                map.Add("K3", "V3");
                map.Add("K3", "V3_wrong");

                // This should now throw an exception
                await Assert.ThrowsAsync<ArgumentException>(() => map.ToOutput().DataTask);
            });

        // Regression test for https://github.com/pulumi/pulumi-dotnet/issues/456
        [Fact]
        public Task InputMapNull()
            => RunInPreview(async () =>
            {
                ImmutableDictionary<string, string>? nullDict = null;
                var map = (InputMap<string>)nullDict!;

                var data = await map.ToOutput().DataTask;
                Assert.True(data.IsKnown);
                Assert.Null(data.Value);

                var nullDictOutput = Output.Create(nullDict);
                map = (InputMap<string>)nullDictOutput!;

                data = await map.ToOutput().DataTask;
                Assert.True(data.IsKnown);
                Assert.Null(data.Value);
            });

        // Regression test for https://github.com/pulumi/pulumi-dotnet/issues/456
        [Fact]
        public Task InputListNull()
            => RunInPreview(async () =>
            {
                ImmutableArray<string> nullList = default;
                var list = (InputList<string>)nullList;

                var data = await list.ToOutput().DataTask;
                Assert.True(data.IsKnown);
                Assert.True(data.Value.IsDefault);

                var nullListOutput = Output.Create(nullList);
                list = (InputList<string>)nullListOutput!;

                data = await list.ToOutput().DataTask;
                Assert.True(data.IsKnown);
                Assert.True(data.Value.IsDefault);
            });

        private class SampleArgs
        {
            public readonly InputList<Union<string, int>> List = new InputList<Union<string, int>>();
            public readonly InputMap<Union<string, int>> Dict = new InputMap<Union<string, int>>();
            public InputUnion<string, int> Union = new InputUnion<string, int>();
        }
    }
}

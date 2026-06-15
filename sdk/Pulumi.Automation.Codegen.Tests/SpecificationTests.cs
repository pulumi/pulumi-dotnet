// Copyright 2016-2026, Pulumi Corporation

using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Pulumi.Automation.Codegen;
using Xunit;

namespace Pulumi.Automation.Codegen.Tests
{
    public class SpecificationTests
    {
        internal static string TestDataDirectory([CallerFilePath] string callerFilePath = "")
            => Path.Combine(Path.GetDirectoryName(callerFilePath)!, "testdata");

        internal static CommandTreeNode LoadFixture()
            => Specification.Load(Path.Combine(TestDataDirectory(), "fixture.json"));

        [Fact]
        public void Load_ParsesTheRootMenu()
        {
            var root = Assert.IsType<MenuNode>(LoadFixture());

            Assert.False(root.Executable);
            Assert.NotNull(root.Flags);
            Assert.True(root.Flags!["color"].Omit);
            Assert.Equal("int", root.Flags["verbose"].Type);
            Assert.Equal("Enable verbose logging", root.Flags["verbose"].Description);
        }

        [Fact]
        public void Load_ParsesCommandsWithPresetsAndArguments()
        {
            var root = Assert.IsType<MenuNode>(LoadFixture());
            var cancel = Assert.IsType<CommandNode>(root.Commands!["cancel"]);

            var yes = cancel.Flags!["yes"];
            Assert.True(yes.Omit);
            Assert.NotNull(yes.Preset);
            Assert.True(yes.Preset!.Value.GetBoolean());

            Assert.NotNull(cancel.Arguments);
            var argument = Assert.Single(cancel.Arguments!.Arguments);
            Assert.Equal("stack-name", argument.Name);
            Assert.Equal(0, cancel.Arguments.RequiredArguments);
            Assert.False(cancel.Arguments.Variadic);
        }

        [Fact]
        public void Load_ParsesExecutableMenusAndRepeatableFlags()
        {
            var root = Assert.IsType<MenuNode>(LoadFixture());
            var org = Assert.IsType<MenuNode>(root.Commands!["org"]);
            Assert.True(org.Executable);

            var search = Assert.IsType<MenuNode>(org.Commands!["search"]);
            Assert.True(search.Executable);
            Assert.True(search.Flags!["query"].Repeatable);

            // `org search ai` redefines the inherited repeatable query flag
            // as a plain string.
            var ai = Assert.IsType<CommandNode>(search.Commands!["ai"]);
            Assert.False(ai.Flags!["query"].Repeatable);
        }

        [Fact]
        public void Load_ParsesVariadicAndRequiredArguments()
        {
            var root = Assert.IsType<MenuNode>(LoadFixture());

            var state = Assert.IsType<MenuNode>(root.Commands!["state"]);
            Assert.False(state.Executable);
            var move = Assert.IsType<CommandNode>(state.Commands!["move"]);
            Assert.True(move.Arguments!.Variadic);

            var org = Assert.IsType<MenuNode>(root.Commands!["org"]);
            var setDefault = Assert.IsType<CommandNode>(org.Commands!["set-default"]);
            Assert.Equal(1, setDefault.Arguments!.RequiredArguments);
        }

        [Fact]
        public void Parse_RejectsUnknownNodeTypes()
        {
            var exception = Assert.Throws<JsonException>(() => Specification.Parse("{\"type\": \"widget\"}"));
            Assert.Contains("widget", exception.Message);
        }

        [Fact]
        public void Parse_RejectsNodesWithoutAType()
            => Assert.Throws<JsonException>(() => Specification.Parse("{}"));
    }
}

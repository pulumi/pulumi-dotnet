// Copyright 2016-2026, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pulumi.Automation.Codegen
{
    /// <summary>
    /// A single CLI flag, as described by `pulumi generate-cli-spec`.
    /// </summary>
    public sealed record Flag
    {
        /// <summary>
        /// The canonical (kebab-case) flag name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// The primitive type of the flag: "string", "boolean" or "int".
        /// </summary>
        public required string Type { get; init; }

        /// <summary>
        /// The help text for the flag.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// True if the flag must be provided.
        /// </summary>
        public bool Required { get; init; }

        /// <summary>
        /// True if the flag may be passed multiple times.
        /// </summary>
        public bool Repeatable { get; init; }

        /// <summary>
        /// True if the flag should not be exposed in generated options types.
        /// </summary>
        public bool Omit { get; init; }

        /// <summary>
        /// If set, the value this flag should be preset to when invoking the
        /// CLI. The JSON type depends on the flag type: boolean, string,
        /// number, or an array of strings.
        /// </summary>
        public JsonElement? Preset { get; init; }

        /// <summary>
        /// Returns a copy of this flag with the override information (omit and
        /// preset) stripped, so that overrides do not leak from parent to
        /// child nodes via inheritance.
        /// </summary>
        public Flag WithoutOverrides()
            => this with { Omit = false, Preset = null };
    }

    /// <summary>
    /// A positional argument to a CLI command.
    /// </summary>
    public sealed record PositionalArgument
    {
        /// <summary>
        /// The (kebab-case) name of the argument.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// The primitive type of the argument. Defaults to "string".
        /// </summary>
        public string Type { get; init; } = "string";
    }

    /// <summary>
    /// The positional arguments accepted by a CLI command.
    /// </summary>
    public sealed record CommandArguments
    {
        /// <summary>
        /// The ordered list of positional arguments.
        /// </summary>
        public List<PositionalArgument> Arguments { get; init; } = new();

        /// <summary>
        /// The number of leading arguments that are required.
        /// </summary>
        public int RequiredArguments { get; init; }

        /// <summary>
        /// True if the final argument may be passed multiple times.
        /// </summary>
        public bool Variadic { get; init; }
    }

    /// <summary>
    /// A node in the CLI command tree: either a <see cref="MenuNode"/> (a set
    /// of subcommands) or a <see cref="CommandNode"/> (a leaf command).
    /// </summary>
    [JsonConverter(typeof(CommandTreeNodeConverter))]
    public abstract record CommandTreeNode
    {
        /// <summary>
        /// Flags declared at this node, keyed by flag name. Inherited flags
        /// are not included unless an override re-declares them here.
        /// </summary>
        public Dictionary<string, Flag>? Flags { get; init; }
    }

    /// <summary>
    /// A set of subcommands, possibly also executable as a command itself.
    /// </summary>
    public sealed record MenuNode : CommandTreeNode
    {
        /// <summary>
        /// True if this menu is also directly executable as a command.
        /// </summary>
        public bool Executable { get; init; }

        /// <summary>
        /// The subcommands of this menu, keyed by command name.
        /// </summary>
        public Dictionary<string, CommandTreeNode>? Commands { get; init; }
    }

    /// <summary>
    /// A leaf command in the CLI.
    /// </summary>
    public sealed record CommandNode : CommandTreeNode
    {
        /// <summary>
        /// The positional arguments accepted by the command.
        /// </summary>
        public CommandArguments? Arguments { get; init; }

        /// <summary>
        /// The help text for the command.
        /// </summary>
        public string? Description { get; init; }
    }

    /// <summary>
    /// Deserializes <see cref="CommandTreeNode"/> values using the "type"
    /// property ("menu" or "command") as a discriminator.
    /// </summary>
    public sealed class CommandTreeNodeConverter : JsonConverter<CommandTreeNode>
    {
        public override CommandTreeNode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);

            if (!document.RootElement.TryGetProperty("type", out var typeProperty))
            {
                throw new JsonException("Command tree node is missing the \"type\" property.");
            }

            var type = typeProperty.GetString();
            var json = document.RootElement.GetRawText();
            return type switch
            {
                "menu" => JsonSerializer.Deserialize<MenuNode>(json, options)!,
                "command" => JsonSerializer.Deserialize<CommandNode>(json, options)!,
                _ => throw new JsonException($"Unknown command tree node type: \"{type}\"."),
            };
        }

        public override void Write(Utf8JsonWriter writer, CommandTreeNode value, JsonSerializerOptions options)
            => throw new NotSupportedException("Serializing command tree nodes is not supported.");
    }

    /// <summary>
    /// Loads CLI specifications produced by `pulumi generate-cli-spec`.
    /// </summary>
    public static class Specification
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        /// <summary>
        /// Parses a CLI specification from its JSON text.
        /// </summary>
        public static CommandTreeNode Parse(string json)
            => JsonSerializer.Deserialize<CommandTreeNode>(json, SerializerOptions)
               ?? throw new JsonException("The CLI specification is empty.");

        /// <summary>
        /// Loads a CLI specification from a JSON file.
        /// </summary>
        public static CommandTreeNode Load(string path)
            => Parse(File.ReadAllText(path));
    }
}

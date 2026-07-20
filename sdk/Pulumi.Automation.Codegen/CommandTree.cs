// Copyright 2016-2026, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Linq;

namespace Pulumi.Automation.Codegen
{
    /// <summary>
    /// An executable CLI command with its flags fully resolved: the flags
    /// declared on the command itself plus those inherited from its ancestors,
    /// with the closest declaration winning when names collide. The override
    /// information (omit/preset) carried by each flag is the one in effect at
    /// this command's node.
    /// </summary>
    public sealed record ResolvedCommand
    {
        /// <summary>
        /// The subcommand chain identifying the command, e.g.
        /// <c>["org", "search", "ai"]</c>.
        /// </summary>
        public required IReadOnlyList<string> Breadcrumbs { get; init; }

        /// <summary>
        /// The resolved flags keyed by flag name, including omitted ones (a
        /// preset may be carried by an omitted flag).
        /// </summary>
        public required IReadOnlyDictionary<string, Flag> Flags { get; init; }

        /// <summary>
        /// The positional arguments accepted by the command, if any.
        /// </summary>
        public CommandArguments? Arguments { get; init; }

        /// <summary>
        /// The CLI command this node represents, e.g. <c>pulumi org search ai</c>.
        /// </summary>
        public string CliCommand => "pulumi " + string.Join(" ", Breadcrumbs);

        /// <summary>
        /// The PascalCase identifier derived from the breadcrumbs, used for
        /// both the method name and (with affixes) the options class name.
        /// </summary>
        public string Identifier => Names.PascalCase(Breadcrumbs);

        /// <summary>
        /// The name of the options class for this command, e.g.
        /// <c>PulumiOrgSearchAIOptions</c>.
        /// </summary>
        public string OptionsClassName => "Pulumi" + Identifier + "Options";

        /// <summary>
        /// The name of the generated method for this command, e.g.
        /// <c>OrgSearchAIAsync</c>. Task-returning methods carry the Async
        /// suffix, as every method on the hand-written Automation API does.
        /// </summary>
        public string MethodName => Identifier + "Async";

        /// <summary>
        /// The flags exposed to users, sorted by name: every resolved flag
        /// that is not omitted.
        /// </summary>
        public IReadOnlyList<Flag> VisibleFlags
            => Flags.Values
                .Where(flag => !flag.Omit)
                .OrderBy(flag => flag.Name, StringComparer.Ordinal)
                .ToList();

        /// <summary>
        /// The flags carrying a preset value, sorted by name.
        /// </summary>
        public IReadOnlyList<Flag> PresetFlags
            => Flags.Values
                .Where(flag => flag.Preset != null)
                .OrderBy(flag => flag.Name, StringComparer.Ordinal)
                .ToList();
    }

    /// <summary>
    /// Walks a CLI specification and yields one <see cref="ResolvedCommand"/>
    /// per executable command, flattening inherited flags on the way down.
    /// Shared by the options and command generators so the two stay in sync.
    /// </summary>
    public static class CommandTreeWalker
    {
        /// <summary>
        /// Returns the executable commands of the specification in a stable
        /// pre-order: each node precedes its children, and siblings are
        /// ordered by name.
        /// </summary>
        public static IReadOnlyList<ResolvedCommand> Walk(CommandTreeNode root)
        {
            var commands = new List<ResolvedCommand>();
            Visit(root, new List<string>(), new Dictionary<string, Flag>(StringComparer.Ordinal), commands);
            return commands;
        }

        private static void Visit(
            CommandTreeNode node,
            List<string> breadcrumbs,
            Dictionary<string, Flag> inherited,
            List<ResolvedCommand> commands)
        {
            // Merge inherited flags with this node's local flags. The local
            // (deeper) declaration wins, which lets a command redefine a flag
            // it inherits with a different type.
            var all = new Dictionary<string, Flag>(inherited, StringComparer.Ordinal);
            foreach (var (name, flag) in node.Flags ?? new Dictionary<string, Flag>())
            {
                all[name] = flag;
            }

            var executable = node is not MenuNode menu || menu.Executable;
            if (executable)
            {
                if (breadcrumbs.Count == 0)
                {
                    throw new InvalidOperationException("The root of the CLI specification must not be executable.");
                }

                commands.Add(new ResolvedCommand
                {
                    Breadcrumbs = breadcrumbs.ToList(),
                    Flags = all,
                    Arguments = (node as CommandNode)?.Arguments,
                });
            }

            if (node is MenuNode menuWithChildren && menuWithChildren.Commands != null)
            {
                // Override information applies only at the node that declares
                // it, so strip it before descending.
                var childInherited = all.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.WithoutOverrides(),
                    StringComparer.Ordinal);

                foreach (var (name, child) in menuWithChildren.Commands.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    Visit(child, breadcrumbs.Append(name).ToList(), childInherited, commands);
                }
            }
        }
    }
}

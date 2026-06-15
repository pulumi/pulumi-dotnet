// Copyright 2016-2026, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace Pulumi.Automation.Codegen
{
    /// <summary>
    /// Converts the kebab-case names used by the CLI specification (flag
    /// names, command names, argument names) into .NET identifiers.
    /// </summary>
    public static class Names
    {
        // Two-letter acronyms are fully capitalized per the .NET naming
        // guidelines ("org search ai" => "OrgSearchAI"). Acronyms of three or
        // more letters are Pascal-cased by the default rule ("url" => "Url"),
        // and "id" is treated as a word ("Id"), so neither needs an entry.
        private static readonly HashSet<string> TwoLetterAcronyms = new(StringComparer.OrdinalIgnoreCase)
        {
            "ai",
            "io",
            "ip",
            "ui",
            "vm",
        };

        /// <summary>
        /// Converts a kebab-case name into a PascalCase identifier:
        /// "include-parents" => "IncludeParents".
        /// </summary>
        public static string PascalCase(string name)
            => string.Concat(SplitWords(name).Select(PascalWord));

        /// <summary>
        /// Converts a sequence of kebab-case names into a single PascalCase
        /// identifier: ["org", "search", "ai"] => "OrgSearchAI".
        /// </summary>
        public static string PascalCase(IEnumerable<string> names)
            => string.Concat(names.Select(PascalCase));

        /// <summary>
        /// Converts a kebab-case name into a camelCase identifier, escaping
        /// C# keywords: "stack-name" => "stackName", "new" => "@new".
        /// </summary>
        public static string CamelCase(string name)
        {
            var words = SplitWords(name);
            if (words.Count == 0)
            {
                return "";
            }

            var first = TwoLetterAcronyms.Contains(words[0])
                ? words[0].ToLowerInvariant()
                : char.ToLowerInvariant(words[0][0]) + words[0][1..];

            return EscapeIdentifier(first + string.Concat(words.Skip(1).Select(PascalWord)));
        }

        /// <summary>
        /// Prefixes an identifier with "@" if it is a reserved C# keyword.
        /// </summary>
        public static string EscapeIdentifier(string identifier)
            => SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None ? identifier : "@" + identifier;

        private static List<string> SplitWords(string name)
            => name.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        private static string PascalWord(string word)
            => TwoLetterAcronyms.Contains(word)
                ? word.ToUpperInvariant()
                : char.ToUpperInvariant(word[0]) + word[1..];
    }
}

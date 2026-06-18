// Copyright 2016-2026, Pulumi Corporation

using System;

namespace Pulumi.Automation.Codegen
{
    /// <summary>
    /// Maps the primitive type system of the CLI specification onto C# types.
    /// </summary>
    internal static class Types
    {
        /// <summary>
        /// The C# type of the scalar a flag or argument carries: <c>string</c>,
        /// <c>bool</c> or <c>int</c>.
        /// </summary>
        public static string Scalar(string specType, string flagName)
            => specType switch
            {
                "string" => "string",
                "boolean" => "bool",
                "int" => "int",
                _ => throw new InvalidOperationException($"Unknown flag type: \"{specType}\" (flag --{flagName})."),
            };

        /// <summary>
        /// The C# type of the option-bag property backing a flag: the scalar
        /// type made nullable, wrapped in <c>List&lt;&gt;</c> for repeatable
        /// flags.
        /// </summary>
        public static string OptionProperty(Flag flag)
        {
            var scalar = Scalar(flag.Type, flag.Name);
            return flag.Repeatable ? $"List<{scalar}>?" : $"{scalar}?";
        }
    }
}

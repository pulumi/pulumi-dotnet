// Copyright 2016-2026, Pulumi Corporation

using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Pulumi.Automation.Codegen
{
    internal static class Program
    {
        private const string Usage =
            "Usage: Pulumi.Automation.Codegen <specification.json> [output-dir] [namespace]";

        private const string DefaultNamespace = "Pulumi.Automation.Interface";

        internal static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine(Usage);
                return 1;
            }

            var specificationPath = Path.GetFullPath(args[0]);
            if (!File.Exists(specificationPath))
            {
                Console.Error.WriteLine($"Specification file not found: {specificationPath}");
                return 1;
            }

            var outputDirectory = args.Length >= 2
                ? Path.GetFullPath(args[1])
                : Path.Combine(Environment.CurrentDirectory, "output");
            var namespaceName = args.Length >= 3 ? args[2] : DefaultNamespace;

            var root = Specification.Load(specificationPath);
            var source = OptionsGenerator.Generate(root, namespaceName);

            // Fail fast if the generated source is not syntactically valid C#
            // rather than letting a broken file reach a consuming project.
            var errors = CSharpSyntaxTree.ParseText(source)
                .GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToList();
            if (errors.Count > 0)
            {
                Console.Error.WriteLine("The generated source is not valid C#:");
                foreach (var error in errors)
                {
                    Console.Error.WriteLine($"  {error}");
                }

                return 1;
            }

            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, "Options.cs");
            File.WriteAllText(outputPath, source);
            Console.WriteLine($"Wrote {outputPath}");

            return 0;
        }
    }
}

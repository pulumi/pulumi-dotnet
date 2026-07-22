// Copyright 2016-2026, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Pulumi.Automation.Codegen;

namespace Pulumi.Automation.Codegen.Tests
{
    /// <summary>
    /// Test helpers for compiling and exercising the generated source.
    /// </summary>
    internal static class GeneratedCode
    {
        public const string Namespace = "Pulumi.Automation.Commands";

        /// <summary>
        /// The testing boilerplate: the half of the API partial class that
        /// records the argument vector instead of running the CLI.
        /// </summary>
        public static string TestingBoilerplate()
            => File.ReadAllText(Path.Combine(BoilerplateDirectory(), "Testing.cs"));

        /// <summary>
        /// Compiles the given C# sources into an in-memory assembly.
        /// </summary>
        public static Compilation Compile(params string[] sources)
        {
            var trees = sources.Select(source => CSharpSyntaxTree.ParseText(source));
            return CSharpCompilation.Create(
                "Pulumi.Automation.Codegen.Generated",
                trees,
                References(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }

        /// <summary>
        /// Generates and compiles the options and command sources for a
        /// specification against the testing boilerplate, then loads the
        /// result so its types can be invoked.
        /// </summary>
        public static Assembly Build(CommandTreeNode specification)
        {
            // BaseOptions and CommandResult come from the referenced Pulumi.Automation.
            var compilation = Compile(
                TestingBoilerplate(),
                OptionsGenerator.Generate(specification, Namespace),
                CommandsGenerator.Generate(specification, Namespace));

            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            if (!result.Success)
            {
                var errors = result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
                throw new InvalidOperationException(
                    "The generated source did not compile:\n" + string.Join("\n", errors));
            }

            return Assembly.Load(stream.ToArray());
        }

        /// <summary>
        /// The metadata references needed to compile the generated source: the
        /// core runtime assemblies.
        /// </summary>
        public static IReadOnlyList<MetadataReference> References()
        {
            var trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
            var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "System.Private.CoreLib",
                "System.Runtime",
                "System.Collections",
                "System.Threading.Tasks",
                "netstandard",
            };

            var references = trustedAssemblies.Split(Path.PathSeparator)
                .Where(path => wanted.Contains(Path.GetFileNameWithoutExtension(path)))
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
                .ToList();

            // The generated commands return the SDK's CommandResult.
            references.Add(MetadataReference.CreateFromFile(
                typeof(global::Pulumi.Automation.Commands.CommandResult).Assembly.Location));

            return references;
        }

        private static string BoilerplateDirectory([CallerFilePath] string callerFilePath = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(callerFilePath)!, "..", "Pulumi.Automation.Codegen", "boilerplate"));
    }

    /// <summary>
    /// A thin reflection wrapper over a compiled generated <c>API</c> instance,
    /// so tests can invoke the generated command methods.
    /// </summary>
    internal sealed class GeneratedApi
    {
        private readonly Assembly _assembly;
        private readonly Type _apiType;
        private readonly object _api;

        public GeneratedApi(CommandTreeNode specification)
        {
            _assembly = GeneratedCode.Build(specification);
            _apiType = _assembly.GetType($"{GeneratedCode.Namespace}.API")
                ?? throw new InvalidOperationException("The generated source does not define an API class.");
            _api = Activator.CreateInstance(_apiType)!;
        }

        /// <summary>
        /// Builds an options bag of the named type with the given properties
        /// set.
        /// </summary>
        public object Options(string typeName, params (string Name, object? Value)[] properties)
        {
            var type = _assembly.GetType($"{GeneratedCode.Namespace}.{typeName}")
                ?? throw new InvalidOperationException($"The generated source does not define {typeName}.");
            var instance = Activator.CreateInstance(type)!;
            foreach (var (name, value) in properties)
            {
                var property = type.GetProperty(name)
                    ?? throw new InvalidOperationException($"{typeName} has no property {name}.");
                property.SetValue(instance, value);
            }

            return instance;
        }

        /// <summary>
        /// Invokes the named command method with the given arguments and
        /// returns the argument vector it built (captured by the testing
        /// boilerplate). A default CancellationToken is appended automatically.
        /// </summary>
        public IReadOnlyList<string> Invoke(string method, params object?[] arguments)
        {
            var methodInfo = _apiType.GetMethod(method)
                ?? throw new InvalidOperationException($"The generated API has no method {method}.");

            var withToken = arguments.Append(System.Threading.CancellationToken.None).ToArray();
            var task = (Task)methodInfo.Invoke(_api, withToken)!;
            task.GetAwaiter().GetResult();

            var lastArguments = _apiType.GetProperty("LastArguments")!.GetValue(_api);
            return (IReadOnlyList<string>)lastArguments!;
        }

        /// <summary>
        /// Reads a public property of the API instance (e.g. the testing
        /// boilerplate's LastOptions).
        /// </summary>
        public object? Read(string property)
            => _apiType.GetProperty(property)!.GetValue(_api);
    }
}

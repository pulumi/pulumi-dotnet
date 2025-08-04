using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Humanizer;

namespace Pulumi.Experimental.Provider
{
    /// <summary>
    /// A host to serve the component provider automatically. See <see cref="Serve(string[], Assembly?, string?)"/> for more details.
    /// </summary>
    public static class ComponentProviderHost
    {
        /// <summary>
        /// Serves the component provider. It discovers all component types in the given assembly and serves them
        /// automatically as a component provider, including GetSchema and Construct methods.
        /// </summary>
        /// <param name="args">The command-line arguments</param>
        /// <param name="componentAssembly">The assembly containing component types</param>
        /// <param name="packageName">Optional package name (defaults to assembly name)</param>
        public static Task Serve(string[] args, Assembly? componentAssembly = null, string? packageName = null)
        {
            var assembly = componentAssembly ?? Assembly.GetCallingAssembly();
            (var parsedNamespace, var parsedPackage) = ParseAssemblyName(assembly.GetName().Name);
            packageName = packageName ?? parsedPackage ?? throw new ArgumentNullException(nameof(packageName));
            // Default the version to "0.0.0" for now, otherwise SDK codegen gets confused without a version.
            var version = "0.0.0";
            var metadata = new Metadata(Name: packageName, Namespace: parsedNamespace, Version: version);
            return Provider.Serve(args, version, host => new ComponentProvider(assembly, metadata), CancellationToken.None);
        }

        internal static (string? namespaceName, string? packageName) ParseAssemblyName(string? assemblyName)
        {
            if (assemblyName == null)
            {
                return (null, null);
            }

            var parts = assemblyName.Split('.');
            if (parts.Length < 2)
            {
                return (null, assemblyName.Kebaberize());
            }

            var package = parts[^1]; // Take last part
            var ns = parts[0]; // Take first part

            return (ns.Kebaberize(), package.Kebaberize());
        }
    }
}

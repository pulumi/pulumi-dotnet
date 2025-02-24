using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pulumi.Utilities;

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
            return Provider.Serve(args, null, host => new ComponentProvider(assembly, packageName), CancellationToken.None);
        }
    }
}

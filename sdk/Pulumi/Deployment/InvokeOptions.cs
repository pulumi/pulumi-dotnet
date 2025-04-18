// Copyright 2016-2019, Pulumi Corporation

namespace Pulumi
{
    /// <summary>
    /// Options to help control the behavior of <see cref="IDeployment.InvokeAsync{T}(string, InvokeArgs, InvokeOptions, RegisterPackageRequest)"/>.
    /// </summary>
    public class InvokeOptions
    {
        /// <summary>
        /// An optional parent to use for default options for this invoke (e.g. the default provider
        /// to use).
        /// </summary>
        public Resource? Parent { get; set; }

        /// <summary>
        /// An optional provider to use for this invocation. If no provider is supplied, the default
        /// provider for the invoked function's package will be used.
        /// </summary>
        public ProviderResource? Provider { get; set; }

        /// <summary>
        /// An optional version, corresponding to the version of the provider plugin that should be
        /// used when performing this invoke.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// An optional URL, corresponding to the download URL of the provider plugin that should be
        /// used when performing this invoke.
        /// </summary>
        public string? PluginDownloadURL { get; set; }
    }

    /// <summary>
    /// Options to help control the behavior of <see cref="IDeployment.Invoke{T}(string, InvokeArgs, InvokeOutputOptions, RegisterPackageRequest)"/>.
    /// </summary>
    public class InvokeOutputOptions : InvokeOptions
    {
        private InputList<Resource>? _dependsOn;

        /// <summary>
        /// Optional additional explicit dependencies on resources.
        /// </summary>
        public InputList<Resource> DependsOn
        {
            get => _dependsOn ??= new InputList<Resource>();
            set => _dependsOn = value;
        }
    }
}

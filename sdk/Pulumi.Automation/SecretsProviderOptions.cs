// Copyright 2016-2021, Pulumi Corporation

namespace Pulumi.Automation
{
    /// <summary>
    /// Options to pass into <see cref="WorkspaceStack.ChangeSecretsProviderAsync(string, SecretsProviderOptions, System.Threading.CancellationToken)"/>.
    /// </summary>
    public class SecretsProviderOptions
    {
        /// <summary>
        /// The new Passphrase to use in the passphrase provider
        /// </summary>
        public string? NewPassphrase { get; set; }
    }
}

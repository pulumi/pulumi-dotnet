// Copyright 2016-2025, Pulumi Corporation

namespace Pulumi.Automation
{
    /// <summary>
    /// Options to pass into <see cref="Workspace.RemoveStackAsync(string, RemoveStackOptions, System.Threading.CancellationToken)"/>.
    /// </summary>
    public class RemoveStackOptions
    {
        /// <summary>
        /// Forces deletion of the stack, leaving behind any resources managed by the stack
        /// </summary>
        public bool Force { get; set; }

        /// <summary>
        /// Do not delete the corresponding Pulumi.&lt;stack-name&gt;.yaml configuration file for the stack
        /// </summary>
        public bool PreserveConfig { get; set; }

        /// <summary>
        /// Remove backups of the stack, if using the DIY backend
        /// </summary>
        public bool RemoveBackups { get; set; }
    }
}

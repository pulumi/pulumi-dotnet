// Copyright 2016-2021, Pulumi Corporation

namespace Pulumi.Automation
{
    /// <summary>
    /// Options controlling the behavior of an <see cref="WorkspaceStack.DestroyAsync(DestroyOptions, System.Threading.CancellationToken)"/> operation.
    /// </summary>
    public sealed class DestroyOptions : UpdateOptions
    {
        public bool? ExcludeDependents { get; set; }

        public bool? TargetDependents { get; set; }

        /// <summary>
        /// Show config secrets when they appear.
        /// </summary>
        public bool? ShowSecrets { get; set; }

        /// <summary>
        /// Only show a preview of the destroy, but don't perform the destroy itself.
        /// </summary>
        public bool? PreviewOnly { get; set; }

        /// <summary>
        /// Continue to perform the destroy operation despite the occurrence of errors.
        /// </summary>
        public bool? ContinueOnError { get; set; }

        /// <summary>
        /// Refresh the state of the stack's resources before this destroy.
        /// </summary>
        public bool? Refresh { get; set; }

        /// <summary>
        /// Runs the program in the workspace to perform the destroy.
        /// </summary>
        public bool? RunProgram { get; set; }
    }
}

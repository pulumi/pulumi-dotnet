// Copyright 2016-2021, Pulumi Corporation

using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Pulumi.Automation
{
    /// <summary>
    /// Options controlling the behavior of an <see cref="WorkspaceStack.UpAsync(UpOptions, System.Threading.CancellationToken)"/> operation.
    /// </summary>
    public sealed class UpOptions : UpdateOptions
    {
        public bool? ExpectNoChanges { get; set; }

        public bool? Diff { get; set; }

        public List<string>? Replace { get; set; }

        public bool? ExcludeDependents { get; set; }

        public bool? TargetDependents { get; set; }

        public PulumiFn? Program { get; set; }

        /// <summary>
        /// Plan specifies the path to an update plan to use for the update.
        /// </summary>
        public string? Plan { get; set; }

        /// <summary>
        /// Show config secrets when they appear.
        /// </summary>
        public bool? ShowSecrets { get; set; }

        /// <summary>
        /// A custom logger instance that will be used for the action. Note that it will only be used
        /// if <see cref="Program"/> is also provided.
        /// </summary>
        public ILogger? Logger { get; set; }

        /// <summary>
        /// Continue to perform the update operation despite the occurrence of errors.
        /// </summary>
        public bool? ContinueOnError { get; set; }

        /// <summary>
        /// Refresh the state of the stack's resources before this update.
        /// </summary>
        public bool? Refresh { get; set; }

        /// <summary>
        /// Show resources that are being read in, alongside those being managed directly in the stack.
        /// </summary>
        public bool? ShowReads { get; set; }
    }
}

// Copyright 2016-2021, Pulumi Corporation

using System.Collections.Generic;

namespace Pulumi.Automation
{
    /// <summary>
    /// Options controlling the behavior of an <see cref="WorkspaceStack.RefreshAsync(RefreshOptions, System.Threading.CancellationToken)"/> operation.
    /// </summary>
    public sealed class RefreshOptions : UpdateOptions
    {
        /// <summary>
        /// Only show a preview of the refresh, but don't perform the refresh
        /// itself.
        /// </summary>
        public bool? PreviewOnly { get; set }

        public bool? ExpectNoChanges { get; set; }

        /// <summary>
        /// Show config secrets when they appear.
        /// </summary>
        public bool? ShowSecrets { get; set; }

        /// <summary>
        /// Ignores any pending create operations
        /// </summary>
        public bool? SkipPendingCreates { get; set; }

        /// <summary>
        /// Removes any pending create operations from the stack
        /// </summary>
        public bool? ClearPendingCreates { get; set; }

        /// <summary>
        /// <see cref="PendingCreateValue"/> values to import into the stack
        /// </summary>
        public List<PendingCreateValue>? ImportPendingCreates { get; set; }
    }
}

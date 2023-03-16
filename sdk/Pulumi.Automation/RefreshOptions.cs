// Copyright 2016-2021, Pulumi Corporation

using System.Collections.Generic;

namespace Pulumi.Automation
{
    /// <summary>
    /// Options controlling the behavior of an <see cref="WorkspaceStack.RefreshAsync(RefreshOptions, System.Threading.CancellationToken)"/> operation.
    /// </summary>
    public sealed class RefreshOptions : UpdateOptions
    {
        public bool? ExpectNoChanges { get; set; }

        /// <summary>
        /// Show config secrets when they appear.
        /// </summary>
        public bool? ShowSecrets { get; set; }

        public bool? SkipPendingCreates { get; set; }

        public bool? ClearPendingCreates { get; set; }

        public List<string>? ImportPendingCreates { get; set; }
    }
}

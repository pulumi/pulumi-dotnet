// Copyright 2016-2023, Pulumi Corporation

using System.Collections.Generic;

namespace Pulumi
{
    /// <summary>
    /// Holds a StackReference's output value.
    /// At most one of Value and SecretValue will be set.
    /// </summary>
    public sealed class StackReferenceOutputDetails
    {
        /// <summary>
        /// Output value returned by the <see cref="StackReference"/>.
        /// This field is only set if the output is not a secret.
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// Secret output value returned by the <see cref="StackReference"/>.
        /// This field is only set if the output is a secret.
        /// </summary>
        public object? SecretValue { get; set; }
    }
}

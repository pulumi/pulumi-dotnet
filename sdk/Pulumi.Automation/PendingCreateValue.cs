// Copyright 2016-2023, Pulumi Corporation

namespace Pulumi.Automation
{
    /// <summary>
    /// Config values for Importing Pending Create operations 
    /// </summary>
    public sealed class PendingCreateValue
    {
        /// <summary>
        /// The logical URN used by Pulumi to identify resources.
        /// </summary>
        public string Urn { get; set; }

        /// <summary>
        /// The Id used by the provider to identify resources.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Creates a <see cref="PendingCreateValue"/> from input values.
        /// </summary>
        /// <param name="urn">The logical URN of a resource used by Pulumi.</param>
        /// <param name="id">The Id of a resource used by the provider.</param>
        public PendingCreateValue(
            string urn,
            string id)
        {
            this.Urn = urn;
            this.Id = id;
        }
    }
}

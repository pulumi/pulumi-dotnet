// Copyright 2016-2020, Pulumi Corporation

using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Pulumi.Testing
{
    /// <summary>
    /// Hooks to mock the engine that provide test doubles for offline unit testing of stacks.
    /// </summary>
    public interface IMocks
    {
        /// <summary>
        /// Invoked when a new resource is created by the program.
        /// </summary>
        /// <param name="args">MockResourceArgs</param>
        /// <returns>A tuple of a resource identifier and resource state. State can be either a POCO
        /// or a dictionary bag. The returned ID may be null for component resources.</returns>
        Task<(string? id, object state)> NewResourceAsync(MockResourceArgs args);

        /// <summary>
        /// Invoked when the program needs to call a provider to load data (e.g., to retrieve an existing
        /// resource).
        /// </summary>
        /// <param name="args">MockCallArgs</param>
        /// <returns>Invocation result, can be either a POCO or a dictionary bag.</returns>
        Task<object> CallAsync(MockCallArgs args);

        /// <summary>
        /// Invoked when component resources (including instances of Stack) register their outputs
        /// </summary>
        /// <param name="args">MockRegisterResourceOutputsRequest</param>
        Task RegisterResourceOutputs(MockRegisterResourceOutputsRequest args) => Task.CompletedTask;
    }

    /// <summary>
    /// MockResourceArgs for use in NewResourceAsync
    /// </summary>
    public class MockResourceArgs
    {
        /// <summary>
        /// Resource type name.
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Resource Name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Dictionary of resource input properties.
        /// </summary>
        public ImmutableDictionary<string, object> Inputs { get; set; } = null!;

        /// <summary>
        /// Provider.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Resource identifier.
        /// </summary>
        public string? Id { get; set; }
    }

    /// <summary>
    /// MockCallArgs for use in CallAsync
    /// </summary>
    public class MockCallArgs
    {
        /// <summary>
        /// Resource identifier.
        /// </summary>
        public string? Token { get; set; }

        /// <summary>
        /// Dictionary of input arguments.
        /// </summary>
        public ImmutableDictionary<string, object> Args { get; set; } = null!;

        /// <summary>
        /// Provider.
        /// </summary>
        public string? Provider { get; set; }
    }

    /// <summary>
    /// MockRegisterResourceOutputsRequest for use in RegisterOutputRequest
    /// </summary>
    public class MockRegisterResourceOutputsRequest
    {
        /// <summary>
        /// The URN of the resource of which the outputs are being registered
        /// </summary>
#pragma warning disable CA1051 // Do not declare visible instance fields
        public readonly string Urn;
#pragma warning restore CA1051 // Do not declare visible instance fields

        /// <summary>
        /// The outputs which have been registered by the resource
        /// </summary>
#pragma warning disable CA1051 // Do not declare visible instance fields
        public readonly ImmutableDictionary<string, Output<object?>> Outputs;
#pragma warning restore CA1051 // Do not declare visible instance fields

        public MockRegisterResourceOutputsRequest(
            string urn,
            ImmutableDictionary<string, Output<object?>> outputs)
        {
            Urn = urn;
            Outputs = outputs;
        }
    }
}

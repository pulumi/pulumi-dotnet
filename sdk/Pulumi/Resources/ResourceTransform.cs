// Copyright 2016-2019, Pulumi Corporation

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Pulumi
{
    /// <summary>
    /// ResourceTransform is the callback signature for <see cref="ResourceOptions.XResourceTransforms"/>. A
    /// transform is passed the same set of inputs provided to the <see cref="Resource"/> constructor, and can
    /// optionally return back alternate values for the <c>properties</c> and/or <c>options</c> prior to the resource
    /// actually being created. The effect will be as though those <c>properties</c> and/or <c>options</c> were passed
    /// in place of the original call to the <see cref="Resource"/> constructor. If the transform returns <see
    /// langword="null"/>, this indicates that the resource will not be transformed.
    /// </summary>
    /// <returns>The new values to use for the <c>args</c> and <c>options</c> of the <see cref="Resource"/> in place of
    /// the originally provided values.</returns>
    public delegate Task<ResourceTransformResult?> ResourceTransform(ResourceTransformArgs args, CancellationToken cancellationToken = default);

    public readonly struct ResourceTransformArgs
    {
        /// <summary>
        /// The name of the resource being transformed.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The type of the resource being transformed.
        /// </summary>
        public string Type { get; }
        /// <summary>
        /// If this is a custom resource.
        /// </summary>
        public bool Custom { get; }
        /// <summary>
        /// The original properties passed to the Resource constructor.
        /// </summary>
        public ImmutableDictionary<string, object?> Args { get; }
        /// <summary>
        /// The original resource options passed to the Resource constructor.
        /// </summary>
        public ResourceOptions Options { get; }

        public ResourceTransformArgs(
            string name, string type, bool custom, ImmutableDictionary<string, object?> args, ResourceOptions options)
        {
            Name = name;
            Type = type;
            Custom = custom;
            Args = args;
            Options = options;
        }
    }

    public readonly struct ResourceTransformResult
    {
        public ImmutableDictionary<string, object?> Args { get; }
        public ResourceOptions Options { get; }

        public ResourceTransformResult(ImmutableDictionary<string, object?> args, ResourceOptions options)
        {
            Args = args;
            Options = options;
        }
    }
}

// Copyright 2016-2024, Pulumi Corporation

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Pulumi
{
    /// <summary>
    /// InvokeTransform is the callback signature for <see cref="StackOptions.InvokeTransforms"/>. A
    /// transform is passed the same set of inputs provided to the <c>Invoke</c>, and can
    /// optionally return back alternate values for the <c>args</c> and/or <c>options</c> prior to the invoke
    /// actually being called. The effect will be as though those args and opts were passed in place
    /// of the original call to the <c>Invoke</c> call. If the transform returns <see langword="null"/>,
    /// this indicates that the invoke will not be transformed.
    /// </summary>
    /// <returns>The new values to use for the <c>args</c> and <c>options</c> of the <c>Invoke</c> in place of
    /// the originally provided values.</returns>
    public delegate Task<InvokeTransformResult?> InvokeTransform(InvokeTransformArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// InvokeTransformArgs is the argument bag passed to an invoke transform.
    /// </summary>
    public readonly struct InvokeTransformArgs
    {
        /// <summary>
        /// The token of the invoke.
        /// </summary>
        public string Token { get; }
        /// <summary>
        /// The original arguments passed to the invocation.
        /// </summary>
        public ImmutableDictionary<string, object?> Args { get; }
        /// <summary>
        /// The original invoke options passed to the invocation.
        /// </summary>
        public InvokeOptions Options { get; }

        public InvokeTransformArgs(
            string token, ImmutableDictionary<string, object?> args, InvokeOptions options)
        {
            Token = token;
            Args = args;
            Options = options;
        }
    }

    /// <summary>
    /// InvokeTransformResult is the result that must be returned by an invoke transform callback.
    /// It includes new values to use for the <c>args</c> and <c>options</c> of the <c>Invoke</c>
    /// in place of the originally provided values.
    /// </summary>
    public readonly struct InvokeTransformResult
    {
        public ImmutableDictionary<string, object?> Args { get; }
        public InvokeOptions Options { get; }

        public InvokeTransformResult(ImmutableDictionary<string, object?> args, InvokeOptions options)
        {
            Args = args;
            Options = options;
        }
    }
}

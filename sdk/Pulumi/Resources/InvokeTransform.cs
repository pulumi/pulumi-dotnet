// Copyright 2016-2019, Pulumi Corporation

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Pulumi
{
    /// <summary>
    /// TODO
    /// </summary>
    /// <returns>
    ///   TODO
    /// </returns>
    public delegate Task<InvokeTransformResult?> InvokeTransform(InvokeTransformArgs args, CancellationToken cancellationToken = default);

    public readonly struct InvokeTransformArgs
    {
        /// <summary>
        /// The token of the invoke.
        /// </summary>
        public string Token { get; }
        /// <summary>
        /// The original properties passed to the Resource constructor.
        /// </summary>
        public ImmutableDictionary<string, object?> Args { get; }
        /// <summary>
        /// The original resource options passed to the Resource constructor.
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

// Copyright 2016-2019, Pulumi Corporation

using System;
using System.Threading.Tasks;

namespace Pulumi
{
    /// <summary>
    /// Internal interface to allow our code to operate on inputs in an untyped manner. Necessary as
    /// there is no reasonable way to write algorithms over heterogeneous instantiations of generic
    /// types.
    /// </summary>
    internal interface IInput
    {
        IOutput ToOutput();
    }

    /// <summary>
    /// <see cref="Input{T}"/> is a property input for a <see cref="Resource"/>.  It may be a promptly
    /// available T, or the output from a existing <see cref="Resource"/>.
    /// </summary>
    public class Input<T> : IInput
    {
        /// <summary>
        /// Technically, in .net we can represent Inputs entirely using the Output type (since
        /// Outputs can wrap values and promises).  However, it would look very weird to state that
        /// the inputs to a resource *had* to be Outputs. So we basically just come up with this
        /// wrapper type so things look sensible, even though under the covers we implement things
        /// using the exact same type
        /// </summary>
        private protected Output<T> _outputValue;

        private protected Input(Output<T> outputValue)
            => _outputValue = outputValue ?? throw new ArgumentNullException(nameof(outputValue));

        public static implicit operator Input<T>(T value)
            => Output.Create(value);

        public static implicit operator Input<T>(Output<T> value)
            => new Input<T>(value);

        public static implicit operator Output<T>(Input<T> input)
            => input._outputValue;

        IOutput IInput.ToOutput()
            => this.ToOutput();
    }

    public static class InputExtensions
    {
        /// <summary>
        /// <see cref="Output{T}.Apply{TResult}(Func{T, Output{TResult}?})"/> for more details.
        /// </summary>
        public static Output<TResult> Apply<T, TResult>(this Input<T>? input, Func<T, TResult> func)
            => input.ToOutput().Apply(func);

        /// <summary>
        /// <see cref="Output{T}.Apply{TResult}(Func{T, Output{TResult}?})"/> for more details.
        /// </summary>
        public static Output<TResult> Apply<T, TResult>(this Input<T>? input, Func<T, Task<TResult>> func)
            => input.ToOutput().Apply(func);

        /// <summary>
        /// <see cref="Output{T}.Apply{TResult}(Func{T, Output{TResult}?})"/> for more details.
        /// </summary>
        public static Output<TResult> Apply<T, TResult>(this Input<T>? input, Func<T, Input<TResult>?> func)
            => input.ToOutput().Apply(func);

        /// <summary>
        /// <see cref="Output{T}.Apply{TResult}(Func{T, Output{TResult}?})"/> for more details.
        /// </summary>
        public static Output<TResult> Apply<T, TResult>(this Input<T>? input, Func<T, Output<TResult>?> func)
            => input.ToOutput().Apply(func);

        public static Output<T> ToOutput<T>(this Input<T>? input)
            => input ?? Output.Create(default(T)!);
    }

    public static class InputListExtensions
    {
        public static void Add<T0, T1>(this InputList<Union<T0, T1>> list, Input<T0> value)
            => list.Add(value.ToOutput().Apply(v => (Union<T0, T1>)v));

        public static void Add<T0, T1>(this InputList<Union<T0, T1>> list, Input<T1> value)
            => list.Add(value.ToOutput().Apply(v => (Union<T0, T1>)v));
    }

    public static class InputMapExtensions
    {
        public static void Add<T0, T1>(this InputMap<Union<T0, T1>> map, string key, Input<T0> value)
            => map.Add(key, value.ToOutput().Apply(v => (Union<T0, T1>)v));

        public static void Add<T0, T1>(this InputMap<Union<T0, T1>> map, string key, Input<T1> value)
            => map.Add(key, value.ToOutput().Apply(v => (Union<T0, T1>)v));
    }
}

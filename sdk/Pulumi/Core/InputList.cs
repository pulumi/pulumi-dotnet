// Copyright 2016-2021, Pulumi Corporation

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Pulumi
{
    /// <summary>
    /// A list of values that can be passed in as the arguments to a <see cref="Resource"/>.
    /// The individual values are themselves <see cref="Input{T}"/>s.  i.e. the individual values
    /// can be concrete values or <see cref="Output{T}"/>s.
    /// <para/>
    /// <see cref="InputList{T}"/> differs from a normal <see cref="IList{T}"/> in that it is itself
    /// an <see cref="Input{T}"/>.  For example, a <see cref="Resource"/> that accepts an <see
    /// cref="InputList{T}"/> will accept not just a list but an <see cref="Output{T}"/>
    /// of a list.  This is important for cases where the <see cref="Output{T}"/>
    /// list from some <see cref="Resource"/> needs to be passed into another <see
    /// cref="Resource"/>.  Or for cases where creating the list invariably produces an <see
    /// cref="Output{T}"/> because its resultant value is dependent on other <see
    /// cref="Output{T}"/>s.
    /// <para/>
    /// This benefit of <see cref="InputList{T}"/> is also a limitation.  Because it represents a
    /// list of values that may eventually be created, there is no way to simply iterate over, or
    /// access the elements of the list synchronously.
    /// <para/>
    /// <see cref="InputList{T}"/> is designed to be easily used in object and collection
    /// initializers.  For example, a resource that accepts a list of inputs can be written in
    /// either of these forms:
    /// <para/>
    /// <code>
    ///     new SomeResource("name", new SomeResourceArgs {
    ///         ListProperty = { Value1, Value2, Value3 },
    ///     });
    /// </code>
    /// <para/>
    /// or
    /// <code>
    ///     new SomeResource("name", new SomeResourceArgs {
    ///         ListProperty = new [] { Value1, Value2, Value3 },
    ///     });
    /// </code>
    /// </summary>
#pragma warning disable CA1010 // Generic interface should also be implemented
#pragma warning disable CA1710 // Identifiers should have correct suffix
    public sealed class InputList<T> : Input<ImmutableArray<T>>, IEnumerable, IAsyncEnumerable<Input<T>>
#pragma warning restore CA1710 // Identifiers should have correct suffix
#pragma warning restore CA1010 // Generic interface should also be implemented
    {
        Input<ImmutableArray<Input<T>>> _inputValue;
        /// <summary>
        /// InputList externally has to behave as an <c>Input{ImmutableArray{T}}</c>, but we actually want to
        /// keep nested Input/Output values separate, so that we can serialise the overall list shape even if one of the
        /// inner elements is an unknown value.
        ///
        /// To do that we keep a separate value of the form <c>Input{ImmutableArray{Input{T}}}</c>/> which each
        /// time we set syncs the flattened value to the base <c>Input{ImmutableArray{T}}</c>.
        /// </summary>
        Input<ImmutableArray<Input<T>>> Value
        {
            get => _inputValue;
            set
            {
                _inputValue = value;
                _outputValue = _inputValue.Apply(inputs => inputs.IsDefault ? Output.Create((ImmutableArray<T>)default) : Output.All(inputs));
            }
        }

        public InputList() : this(ImmutableArray<Input<T>>.Empty)
        {
        }

        private InputList(Input<ImmutableArray<Input<T>>> values)
            : base(values.Apply(values => values.IsDefault ? Output.Create((ImmutableArray<T>)default) : Output.All(values)))
        {
            _inputValue = values;
        }

        public void Add(params Input<T>[] inputs)
        {
            Value = Concat(inputs).Value;
        }

        /// <summary>
        /// Note: this is non-standard convenience for use with collection initializers.
        /// </summary>
        public void Add(InputList<T> inputs)
        {
            AddRange(inputs);
        }

        public void AddRange(InputList<T> inputs)
        {
            Value = Concat(inputs).Value;
        }

        /// <summary>
        /// Concatenates the values in this list with the values in <paramref name="other"/>,
        /// returning the concatenated sequence in a new <see cref="InputList{T}"/>.
        /// </summary>
        public InputList<T> Concat(InputList<T> other)
        {
            var list = new InputList<T>();
            list.Value = Output.Tuple(Value, other.Value).Apply(t =>
            {
                var (first, second) = t;
                return first.AddRange(second);
            });
            return list;
        }

        internal InputList<T> Clone()
            => new InputList<T>(Value);

        #region construct from unary

        public static implicit operator InputList<T>(T value)
            => ImmutableArray.Create<Input<T>>(value);

        public static implicit operator InputList<T>(Output<T> value)
            => ImmutableArray.Create<Input<T>>(value);

        public static implicit operator InputList<T>(Input<T> value)
            => ImmutableArray.Create(value);

        #endregion

        #region construct from array

        public static implicit operator InputList<T>(T[] values)
            => ImmutableArray.CreateRange(values.Select(v => (Input<T>)v));

        public static implicit operator InputList<T>(Output<T>[] values)
            => ImmutableArray.CreateRange(values.Select(v => (Input<T>)v));

        public static implicit operator InputList<T>(Input<T>[] values)
            => ImmutableArray.CreateRange(values);

        #endregion

        #region construct from list

        public static implicit operator InputList<T>(List<T> values)
            => ImmutableArray.CreateRange(values);

        public static implicit operator InputList<T>(List<Output<T>> values)
            => ImmutableArray.CreateRange(values);

        public static implicit operator InputList<T>(List<Input<T>> values)
            => ImmutableArray.CreateRange(values);

        #endregion

        #region construct from immutable array

        public static implicit operator InputList<T>(ImmutableArray<T> values)
            => values.IsDefault ? default : values.SelectAsArray(v => (Input<T>)v);

        public static implicit operator InputList<T>(ImmutableArray<Output<T>> values)
            => values.IsDefault ? default : values.SelectAsArray(v => (Input<T>)v);

        public static implicit operator InputList<T>(ImmutableArray<Input<T>> values)
            => new InputList<T>(values);

        #endregion

        #region construct from Output of some list type.

        public static implicit operator InputList<T>(Output<T[]> values)
            => values.Apply(ImmutableArray.CreateRange);

        public static implicit operator InputList<T>(Output<List<T>> values)
            => values.Apply(ImmutableArray.CreateRange);

        public static implicit operator InputList<T>(Output<IEnumerable<T>> values)
            => values.Apply(ImmutableArray.CreateRange);

        public static implicit operator InputList<T>(Output<ImmutableArray<T>> values)
            => new InputList<T>(values.Apply(values =>
            {
                if (values.IsDefault)
                {
                    return default;
                }

                var builder = ImmutableArray.CreateBuilder<Input<T>>(values.Length);
                foreach (var value in values)
                {
                    builder.Add(value);
                }
                return builder.MoveToImmutable();
            }));

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
            => throw new NotSupportedException($"A {GetType().FullName} cannot be synchronously enumerated. Use {nameof(GetAsyncEnumerator)} instead.");

        public async IAsyncEnumerator<Input<T>> GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            var data = await _outputValue.GetValueAsync(whenUnknown: ImmutableArray<T>.Empty)
                .ConfigureAwait(false);
            foreach (var value in data)
            {
                yield return value;
            }
        }

        #endregion
    }
}

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
    /// A mapping of <see cref="string"/>s to values that can be passed in as the arguments to a
    /// <see cref="Resource"/>. The individual values are themselves <see cref="Input{T}"/>s.  i.e.
    /// the individual values can be concrete values or <see cref="Output{T}"/>s.
    /// <para/>
    /// <see cref="InputMap{V}"/> differs from a normal <see cref="IDictionary{TKey,TValue}"/> in that it is
    /// itself an <see cref="Input{T}"/>.  For example, a <see cref="Resource"/> that accepts an
    /// <see cref="InputMap{V}"/> will accept not just a dictionary but an <see cref="Output{T}"/>
    /// of a dictionary as well.  This is important for cases where the <see cref="Output{T}"/>
    /// map from some <see cref="Resource"/> needs to be passed into another <see cref="Resource"/>.
    /// Or for cases where creating the map invariably produces an <see cref="Output{T}"/> because
    /// its resultant value is dependent on other <see cref="Output{T}"/>s.
    /// <para/>
    /// This benefit of <see cref="InputMap{V}"/> is also a limitation.  Because it represents a
    /// list of values that may eventually be created, there is no way to simply iterate over, or
    /// access the elements of the map synchronously.
    /// <para/>
    /// <see cref="InputMap{V}"/> is designed to be easily used in object and collection
    /// initializers.  For example, a resource that accepts a map of values can be written easily in
    /// this form:
    /// <para/>
    /// <code>
    ///     new SomeResource("name", new SomeResourceArgs {
    ///         MapProperty = {
    ///             { Key1, Value1 },
    ///             { Key2, Value2 },
    ///             { Key3, Value3 },
    ///         },
    ///     });
    /// </code>
    /// </summary>
    public sealed class InputMap<V> : Input<ImmutableDictionary<string, V>>, IEnumerable, IAsyncEnumerable<Input<KeyValuePair<string, V>>>
    {
        private static Input<ImmutableDictionary<string, V>> Flatten(Input<ImmutableDictionary<string, Input<V>>> inputs)
        {
            return inputs.Apply(inputs =>
            {
                var list = inputs.Select(kv => kv.Value.Apply(value => KeyValuePair.Create(kv.Key, value)));
                return Output.All(list).Apply(kvs =>
                {
                    var result = ImmutableDictionary.CreateBuilder<string, V>();
                    foreach (var (k, v) in kvs)
                    {
                        result[k] = v;
                    }
                    return result.ToImmutable();
                });
            });
        }

        Input<ImmutableDictionary<string, Input<V>>> _inputValue;
        Input<ImmutableDictionary<string, Input<V>>> Value
        {
            get => _inputValue;
            set
            {
                _inputValue = value;
                _outputValue = Flatten(_inputValue);
            }
        }

        public InputMap() : this(ImmutableDictionary<string, Input<V>>.Empty)
        {
        }

        private InputMap(Input<ImmutableDictionary<string, Input<V>>> values)
            : base(Flatten(values))
        {
            _inputValue = values;
        }

        public void Add(string key, Input<V> value)
        {
            Value = Value.Apply(self => self.Add(key, value));
        }

        /// <summary>
        /// Note: this is non-standard convenience for use with collection initializers.
        /// </summary>
        public void Add(InputMap<V> values)
        {
            AddRange(values);
        }

        public void AddRange(InputMap<V> values)
        {
            Value = Output.Tuple(Value, values.Value)
                .Apply(x => x.Item1.AddRange(x.Item2));
        }

        public Input<V> this[string key]
        {
            set => Add(key, value);
        }

        /// <summary>
        /// Merge two instances of <see cref="InputMap{V}"/>. Returns a new <see cref="InputMap{V}"/>
        /// without modifying any of the arguments.
        /// <para/>If both maps contain the same key, the value from the second map takes over.
        /// </summary>
        /// <param name="first">The first <see cref="InputMap{V}"/>. Has lower priority in case of
        /// key clash.</param>
        /// <param name="second">The second <see cref="InputMap{V}"/>. Has higher priority in case of
        /// key clash.</param>
        /// <returns>A new instance of <see cref="InputMap{V}"/> that contains the items from
        /// both input maps.</returns>
        public static InputMap<V> Merge(InputMap<V> first, InputMap<V> second)
        {
            var output = Output.Tuple(first.Value, second.Value)
                               .Apply(dicts =>
                               {
                                   var builder = ImmutableDictionary.CreateBuilder<string, Input<V>>();
                                   foreach (var (k, v) in dicts.Item1)
                                       builder[k] = v;
                                   // Overwrite keys if duplicates are found
                                   foreach (var (k, v) in dicts.Item2)
                                       builder[k] = v;
                                   return builder.ToImmutable();
                               });
            return new InputMap<V>(output);
        }

        #region construct from dictionary types

        public static implicit operator InputMap<V>(Dictionary<string, V> values)
            => Output.Create(values);

        public static implicit operator InputMap<V>(ImmutableDictionary<string, V> values)
            => Output.Create(values);

        public static implicit operator InputMap<V>(Output<Dictionary<string, V>> values)
            => values.Apply(ImmutableDictionary.CreateRange);

        public static implicit operator InputMap<V>(Output<IDictionary<string, V>> values)
            => values.Apply(ImmutableDictionary.CreateRange);

        public static implicit operator InputMap<V>(Output<ImmutableDictionary<string, V>> values)
            => new InputMap<V>(values.Apply(values =>
            {
                var builder = ImmutableDictionary.CreateBuilder<string, Input<V>>();
                foreach (var value in values)
                {
                    builder.Add(value.Key, value.Value);
                }
                return builder.ToImmutable();
            }));

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
            => throw new NotSupportedException($"A {GetType().FullName} cannot be synchronously enumerated. Use {nameof(GetAsyncEnumerator)} instead.");

        public async IAsyncEnumerator<Input<KeyValuePair<string, V>>> GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            var data = await _outputValue.GetValueAsync(whenUnknown: ImmutableDictionary<string, V>.Empty)
                .ConfigureAwait(false);
            foreach (var value in data)
            {
                yield return value;
            }
        }

        #endregion
    }
}

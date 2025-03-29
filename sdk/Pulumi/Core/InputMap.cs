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
#pragma warning disable CA1010 // Generic interface should also be implemented
#pragma warning disable CA1710 // Identifiers should have correct suffix
#pragma warning disable CA1715 // Identifiers should have correct prefix
    public sealed class InputMap<V> : Input<ImmutableDictionary<string, V>>, IEnumerable, IAsyncEnumerable<Input<KeyValuePair<string, V>>>
#pragma warning restore CA1715 // Identifiers should have correct prefix
#pragma warning restore CA1710 // Identifiers should have correct suffix
#pragma warning restore CA1010 // Generic interface should also be implemented
    {
        private static Input<ImmutableDictionary<string, V>> Flatten(Input<ImmutableDictionary<string, Input<V>>> inputs)
        {
            return inputs.Apply(inputs =>
            {
                // Backcompat: See the comment in the implicit conversion from Output<ImmutableDictionary<string, V>>.
                if (inputs == null)
                {
                    return null!;
                }

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
        /// <summary>
        /// InputMap externally has to behave as an <c>Input{ImmutableDictionary{string, T}}</c>, but we actually
        /// want to keep nested Input/Output values separate, so that we can serialise the overall map shape even if
        /// one of the inner elements is an unknown value.
        ///
        /// To do that we keep a separate value of the form <c>Input{ImmutableDictionary{string, Input{T}}}</c>
        /// which each time we set syncs the flattened value to the base <c>Input{ImmutableDictionary{string,
        /// T}}</c>.
        /// </summary>
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
            Value = Value.Apply(self =>
            {
                // ImmutableDictionary allows the same key value pair to be added twice. Sameness is decided
                // via EqualityComparer<V>, which used to work well for InputMap but now that we have Input<T>
                // as values, we need to compare the value inside the Input<T> and not the Input<T> itself.
                // See https://github.com/pulumi/pulumi-dotnet/issues/458.

                if (!self.TryGetValue(key, out var existingValue))
                {
                    // If the key is not present, add it
                    return self.Add(key, value);
                }

                // Else we can only know if the key is ok to add if we compare the values inside the Input<T>.
                // This is a bit odd for two reasons.
                // 1) Firstly the exception is only seen if awaiting _this_ value, not the value for the
                //    dictionary itself. That shouldn't be an issue because the only thing that can resolve
                //    the dictionaries inner levels separately is the property serializer and it always reads
                //    all the values.
                // 2) Secondly this merges the secretness and dependency information of the two values, so if
                //    you add "K" with a secret "hello", and then call add for "K" again with a non-secret
                //    "hello" it's going to merge both and keep the secret tag. Doing better then this
                //    requires a custom Apply function here.
                return self.SetItem(key,
                    Output.Tuple(existingValue, value).Apply(x =>
                        EqualityComparer<V>.Default.Equals(x.Item1, x.Item2) ?
                            x.Item1 : throw new ArgumentException($"Key '{key}' already exists in the map with a different value.")));
            });
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
// This has already shipped as static and we're not planning to make a breaking change here.
#pragma warning disable CA1000 // Do not declare static members on generic types
        public static InputMap<V> Merge(InputMap<V> first, InputMap<V> second)
#pragma warning restore CA1000 // Do not declare static members on generic types
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
                // Backwards compatibility: if the immutable dictionary is null just flow through the nullness. This is
                // against the nullability annotations but it used to "work" before
                // https://github.com/pulumi/pulumi-dotnet/pull/449.
                if (values == null)
                {
                    return null!;
                }

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

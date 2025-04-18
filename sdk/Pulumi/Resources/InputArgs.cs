// Copyright 2016-2019, Pulumi Corporation

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Google.Protobuf;
using Pulumi.Serialization;

namespace Pulumi
{
    /// <summary>
    /// Base type for all input argument classes.
    /// </summary>
    public abstract class InputArgs
    {
        private readonly ImmutableArray<InputInfo> _inputInfos;

        private protected abstract void ValidateMember(Type memberType, string fullName);

        protected InputArgs()
        {
            var fieldQuery =
                from field in this.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                let attr = field.GetCustomAttribute<InputAttribute>()
                where attr != null
                select (attr, memberName: field.Name, memberType: field.FieldType, getValue: (Func<object, object?>)field.GetValue);

            var propQuery =
                from prop in this.GetType().GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                let attr = prop.GetCustomAttribute<InputAttribute>()
                where attr != null
                select (attr, memberName: prop.Name, memberType: prop.PropertyType, getValue: (Func<object, object?>)prop.GetValue);

            var all = fieldQuery.Concat(propQuery).ToList();

            foreach (var (_, memberName, memberType, _) in all)
            {
                var fullName = $"[Input] {this.GetType().FullName}.{memberName}";
#pragma warning disable CA2214
                // ReSharper disable once VirtualMemberCallInConstructor
                ValidateMember(memberType, fullName);
#pragma warning restore CA2214
            }

            _inputInfos = all.Select(t =>
                new InputInfo(t.attr!, t.memberName, t.memberType, t.getValue)).ToImmutableArray();
        }

        internal virtual Task<ImmutableDictionary<string, object?>> ToDictionaryAsync()
        {
            var builder = ImmutableDictionary.CreateBuilder<string, object?>();
            foreach (var info in _inputInfos)
            {
                var fullName = $"[Input] {this.GetType().FullName}.{info.MemberName}";

                var value = info.GetValue(this);
                if (info.Attribute.IsRequired && value == null)
                {
                    throw new ArgumentNullException(info.MemberName, $"{fullName} is required but was not given a value");
                }

                if (info.Attribute.Json)
                {
                    value = ConvertToJson(fullName, value);
                }

                builder.Add(info.Attribute.Name, value);
            }

            return Task.FromResult(builder.ToImmutable());
        }

        private static Input<string>? ConvertToJson(string context, object? input)
        {
            if (input == null)
                return null;

            Func<object?, Task<string>> serialize = async (object? input) =>
            {
                var serializer = new Serializer(excessiveDebugOutput: false);
                var obj = await serializer.SerializeAsync(context, input, false).ConfigureAwait(false);
                var value = Serializer.CreateValue(obj);
                return JsonFormatter.Default.Format(value);
            };

            if (input is IInput i)
            {
                var output = i.ToOutput();
                return output.UntypedApply<string>(input => Output.Create(serialize(input)));
            }
            else
            {
                return Output.Create(serialize(input));
            }
        }

        private readonly struct InputInfo
        {
            public readonly InputAttribute Attribute;
            // ReSharper disable once NotAccessedField.Local
            public readonly Type MemberType;
            public readonly string MemberName;
            public readonly Func<object, object?> GetValue;

            public InputInfo(InputAttribute attribute, string memberName, Type memberType, Func<object, object?> getValue) : this()
            {
                Attribute = attribute;
                MemberName = memberName;
                MemberType = memberType;
                GetValue = getValue;
            }
        }
    }
}

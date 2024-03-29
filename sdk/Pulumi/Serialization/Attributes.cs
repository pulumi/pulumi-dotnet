// Copyright 2016-2019, Pulumi Corporation

using System;
using Google.Protobuf.WellKnownTypes;

namespace Pulumi
{
    /// <summary>
    /// Attribute used by a mark <see cref="Resource"/> output properties. Use this attribute
    /// in your Pulumi programs to mark outputs of <see cref="ComponentResource"/> and
    /// <see cref="Stack"/> resources.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class OutputAttribute : Attribute
    {
        public string? Name { get; }

        public OutputAttribute(string? name = null)
        {
            Name = name;
        }
    }

    /// <summary>
    /// Attribute used by a Pulumi Cloud Provider Package to mark <see cref="Resource"/> input
    /// fields and properties.
    /// <para/>
    /// Note: for simple inputs (i.e. <see cref="Input{T}"/> this should just be placed on the
    /// property itself.  i.e. <c>[Input] Input&lt;string&gt; Acl</c>.
    ///
    /// For collection inputs (i.e. <see cref="InputList{T}"/> this should be placed on the
    /// backing field for the property.  i.e.
    ///
    /// <code>
    ///     [Input] private InputList&lt;string&gt; _acls;
    ///     public InputList&lt;string&gt; Acls
    ///     {
    ///         get => _acls ?? (_acls = new InputList&lt;string&gt;());
    ///         set => _acls = value;
    ///     }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class InputAttribute : Attribute
    {
        internal string Name { get; }
        internal bool IsRequired { get; }
        internal bool Json { get; }

        public InputAttribute(string name, bool required = false, bool json = false)
        {
            Name = name;
            IsRequired = required;
            Json = json;
        }
    }

    /// <summary>
    /// Attribute used by a Pulumi Cloud Provider Package to mark complex types used for a Resource
    /// output property.  A complex type must have a single constructor in it marked with the
    /// <see cref="OutputConstructorAttribute"/> attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class OutputTypeAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute used by a Pulumi Cloud Provider Package to marks the constructor for a complex
    /// property type so that it can be instantiated by the Pulumi runtime.
    ///
    /// The constructor should contain parameters that map to the resultant <see
    /// cref="Struct.Fields"/> returned by the engine.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor)]
    public sealed class OutputConstructorAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute used by a Pulumi Cloud Provider Package to mark
    /// constructor parameters with a name override.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class OutputConstructorParameterAttribute : Attribute
    {
        public string Name { get; }

        public OutputConstructorParameterAttribute(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// Attribute used by a Pulumi Cloud Provider Package to mark enum types.
    ///
    /// Requirements for a struct-based enum to be (de)serialized are as follows.
    /// It must:
    ///   * Be a value type (struct) decoratted with EnumTypeAttribute.
    ///   * Have a constructor that takes a single parameter of the underlying type.
    ///     The constructor can be private.
    ///   * Have an explicit conversion operator that converts the enum type to the underlying type.
    ///   * Have an underlying type of String or Double.
    ///   * Implementing IEquatable, adding ==/=! operators and overriding ToString isn't required,
    ///     but is recommended and is what our codegen does.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class EnumTypeAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class ResourceTypeAttribute : Attribute
    {
        public string Type { get; }

        public string? Version { get; }

        public ResourceTypeAttribute(string type, string? version)
        {
            Type = type;
            Version = version;
        }
    }
}

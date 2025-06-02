// Copyright 2016-2019, Pulumi Corporation

using System;
using System.Diagnostics.CodeAnalysis;

namespace Pulumi
{
    /// <summary>
    /// An automatically generated logical URN, used to stably identify resources. These are created
    /// automatically by Pulumi to identify resources.  They cannot be manually constructed.
    /// </summary>
    public readonly struct Urn : IEquatable<Urn>
    {
        private readonly string _value;

        public Urn(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("URN cannot be null or empty.", nameof(value));
            }

            _value = value;
        }

        /// <summary>
        /// Implicitly converts a URN to a string.
        /// </summary>
        public static implicit operator string(Urn value) => value._value;

        /// <summary>
        /// Returns the string representation of this URN.
        /// </summary>
        public override string ToString() => _value;

        /// <summary>
        /// Determines whether the specified object is equal to the current URN.
        /// </summary>
        public override bool Equals([NotNullWhen(true)] object? obj)
            => obj is Urn other && Equals(other);

        /// <summary>
        /// Determines whether the specified URN is equal to the current URN.
        /// </summary>
        public bool Equals(Urn other)
            => string.Equals(_value, other._value, StringComparison.Ordinal);

        /// <summary>
        /// Returns a hash code for this URN.
        /// </summary>
        public override int GetHashCode()
            => _value.GetHashCode(StringComparison.Ordinal);


        /// <summary>
        /// Determines whether two URN values are equal.
        /// </summary>
        public static bool operator ==(Urn left, Urn right)
            => left.Equals(right);

        /// <summary>
        /// Determines whether two URN values are not equal.
        /// </summary>
        public static bool operator !=(Urn left, Urn right)
            => !(left == right);

        /// <summary>
        /// Computes a URN from the combination of a resource name, resource type, optional parent,
        /// optional project and optional stack.
        /// </summary>
        /// <returns></returns>
        public static Output<string> Create(
            Input<string> name, Input<string> type,
            Resource? parent = null, Input<string>? parentUrn = null,
            Input<string>? project = null, Input<string>? stack = null)
        {
            if (parent != null && parentUrn != null)
                throw new ArgumentException("Only one of 'parent' and 'parentUrn' can be non-null.");

            Output<string> parentPrefix;
            if (parent != null || parentUrn != null)
            {
                var parentUrnOutput = parent != null
                    ? parent.Urn
                    : parentUrn!.ToOutput();

                parentPrefix = parentUrnOutput.Apply(s =>
                    string.Concat(s.AsSpan(0, s.LastIndexOf("::", StringComparison.Ordinal)), "$"));
            }
            else
            {
                parentPrefix = Output.Format($"urn:pulumi:{stack ?? Deployment.Instance.StackName}::{project ?? Deployment.Instance.ProjectName}::");
            }

            return Output.Format($"{parentPrefix}{type}::{name}");
        }

        /// <summary>
        /// <see cref="InheritedChildAlias"/> computes the alias that should be applied to a child
        /// based on an alias applied to it's parent. This may involve changing the name of the
        /// resource in cases where the resource has a named derived from the name of the parent,
        /// and the parent name changed.
        /// </summary>
        internal static Output<string> InheritedChildAlias(string childName, string parentName, Input<string> parentAlias, string childType)
        {
            // If the child name has the parent name as a prefix, then we make the assumption that
            // it was constructed from the convention of using '{name}-details' as the name of the
            // child resource.  To ensure this is aliased correctly, we must then also replace the
            // parent aliases name in the prefix of the child resource name.
            //
            // For example:
            // * name: "newapp-function"
            // * options.parent.__name: "newapp"
            // * parentAlias: "urn:pulumi:stackname::projectname::awsx:ec2:Vpc::app"
            // * parentAliasName: "app"
            // * aliasName: "app-function"
            // * childAlias: "urn:pulumi:stackname::projectname::aws:s3/bucket:Bucket::app-function"
            var aliasName = Output.Create(childName);
            if (childName!.StartsWith(parentName, StringComparison.Ordinal))
            {
                aliasName = parentAlias.ToOutput().Apply(s =>
                    string.Concat(s.AsSpan(s.LastIndexOf("::", StringComparison.Ordinal) + 2), childName.AsSpan(parentName.Length)));
            }

            var urn = Create(
                aliasName, childType, parent: null,
                parentUrn: parentAlias, project: null, stack: null);
            return urn;
        }

        internal static string Name(string urn)
        {
            var parts = urn.Split("::");
            return parts[3];
        }

        internal static string Type(string urn)
        {
            var parts = urn.Split("::");
            var typeParts = parts[2].Split("$");
            return typeParts[typeParts.Length - 1];
        }
    }
}

// Copyright 2016-2019, Pulumi Corporation

using System;

namespace Pulumi
{
    /// <summary>
    /// An automatically generated logical URN, used to stably identify resources. These are created
    /// automatically by Pulumi to identify resources.  They cannot be manually constructed.
    /// </summary>
    public class Urn
    {
        private const string Prefix = "urn:pulumi:";
        private const string PartsSeparator = "::";
        private const string PartsSeparatorRegex = "[:][:]";
        private const string TypeSeparator = ":";
        private const string TypeSeparatorRegex = "[:]";
        private const string ParentSeparator = "$";
        private const string ParentSeparatorRegex = "\\$";

        public readonly string Stack;
        public readonly string Project;
        public readonly string QualifiedType;
        public readonly string Name;

        private Urn(string stack, string project, string qualifiedType, string name)
        {
            this.Stack = stack;
            this.Project = project;
            this.QualifiedType = qualifiedType;
            this.Name = name;
        }

        /// <summary>
        /// Computes a URN from the combination of a resource name, resource type, optional parent,
        /// optional project and optional stack.
        /// </summary>
        /// <returns></returns>
        public static Output<string> Create(Input<string> name,
            Input<string> type,
            Resource? parent = null,
            Input<string>? parentUrn = null,
            Input<string>? project = null,
            Input<string>? stack = null)
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

        internal static string GetName(string urn)
        {
            var parts = urn.Split("::");
            return parts[3];
        }

        internal static string GetType(string urn)
        {
            var parts = urn.Split("::");
            var typeParts = parts[2].Split("$");
            return typeParts[^1];
        }

        public static Urn Parse(String urn)
        {
            if (string.IsNullOrEmpty(urn))
            {
                throw new ArgumentException($"expected urn to be not empty and not null, got: '{urn}'");
            }

            if (urn.StartsWith(Prefix, StringComparison.Ordinal))
            {
                throw new ArgumentException($"expected urn to start with '{Prefix}', got: '{urn}'");
            }

            var urnParts = urn.Split(PartsSeparator); // -1 avoids dropping trailing empty strings
            if (urnParts.Length != 4)
            {
                throw new ArgumentException($"expected urn to have 4 parts, separated by '{PartsSeparator}', got '{urnParts.Length}' in '{urn}'");
            }

            var stack = urnParts[0].Substring(Prefix.Length);
            if (string.IsNullOrEmpty(stack))
            {
                throw new ArgumentException($"expected urn stack part to be not empty, got: '{urn}'");
            }

            if (stack.Contains(ParentSeparator))
            {
                throw new ArgumentException($"expected urn stack part to not contain '{ParentSeparator}', got: '{stack}'");
            }

            var project = urnParts[1];
            if (string.IsNullOrEmpty(project))
            {
                throw new ArgumentException($"expected urn project part to be not empty, got: '{urn}'");
            }

            if (project.Contains(ParentSeparator))
            {
                throw new ArgumentException($"expected urn project part to not contain '{ParentSeparator}', got: '{project}'");
            }

            var qualifiedType = urnParts[2];
            if (string.IsNullOrEmpty(qualifiedType))
            {
                throw new ArgumentException($"expected urn qualifiedType part to be not empty, got: '{urn}'");
            }

            var name = urnParts[3];
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException($"expected urn name part to be not empty, got: '{urn}'");
            }

            return new Urn(stack, project, qualifiedType, name);
        }
    }
}

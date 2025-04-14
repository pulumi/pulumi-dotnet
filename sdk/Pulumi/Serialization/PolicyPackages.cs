// Copyright 2016-2020, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Google.Protobuf.Reflection;
using Semver;

namespace Pulumi
{
    public static class PolicyResourcePackages
    {
        private static readonly Lazy<ImmutableList<(Type, PolicyResourceTypeAttribute)>> _resourceInputTypes = new(DiscoverPolicyResourceInputTypes);
        private static readonly Lazy<ImmutableList<(Type, PolicyResourceTypeAttribute)>> _resourceOutputTypes = new(DiscoverPolicyResourceOutputTypes);

        private static ImmutableList<(Type, PolicyResourceTypeAttribute)> DiscoverPolicyResourceInputTypes()
        {
            var pairs =
                from a in ResourcePackages.DiscoverCandidateAssemblies()
                from t in a.GetTypes()
                where typeof(PolicyResourceInput).IsAssignableFrom(t)
                let attr = t.GetCustomAttribute<PolicyResourceTypeAttribute>()
                where attr != null
                select (t, attr);

            return pairs.ToImmutableList();
        }

        private static ImmutableList<(Type, PolicyResourceTypeAttribute)> DiscoverPolicyResourceOutputTypes()
        {
            var pairs =
                from a in ResourcePackages.DiscoverCandidateAssemblies()
                from t in a.GetTypes()
                where typeof(PolicyResourceOutput).IsAssignableFrom(t)
                let attr = t.GetCustomAttribute<PolicyResourceTypeAttribute>()
                where attr != null
                select (t, attr);

            return pairs.ToImmutableList();
        }

        public static Type? ResolveInputType(string type, string? version)
        {
            return ResolveType(_resourceInputTypes.Value, type, version);
        }

        public static Type? ResolveOutputType(string type, string? version)
        {
            return ResolveType(_resourceOutputTypes.Value, type, version);
        }

        private static Type? ResolveType(ImmutableList<(Type, PolicyResourceTypeAttribute)> lst, string type, string? version)
        {
            foreach (var pair in lst)
            {
                if (pair.Item2.Type == type)
                {
                    if (version == null || (pair.Item2.Version ?? "") == version)
                    {
                        return pair.Item1;
                    }
                }
            }

            return null;
        }
    }
}

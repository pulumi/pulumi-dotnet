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
        private static readonly Lazy<ImmutableList<(Type, PolicyResourceTypeAttribute)>> _resourceTypes = new(DiscoverPolicyResourceTypes);

        public static ImmutableList<(Type, PolicyResourceTypeAttribute)> Get()
        {
            return _resourceTypes.Value;
        }

        private static ImmutableList<(Type, PolicyResourceTypeAttribute)> DiscoverPolicyResourceTypes()
        {
            var pairs =
                from a in ResourcePackages.DiscoverCandidateAssemblies()
                from t in a.GetTypes()
                where typeof(PolicyResource).IsAssignableFrom(t)
                let attr = t.GetCustomAttribute<PolicyResourceTypeAttribute>()
                where attr != null
                select (t, attr);

            return pairs.ToImmutableList();
        }

        public static Type? ResolveType(string type, string? version)
        {
            foreach (var pair in _resourceTypes.Value)
            {
                if (pair.Item2.Type == type)
                {
                    if (version == null || pair.Item2.Version == version)
                        return pair.Item1;
                }
            }

            return null;
        }
    }
}

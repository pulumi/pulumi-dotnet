// Copyright 2016-2020, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Google.Protobuf.Reflection;
using Pulumi.Analyzer;
using Semver;

namespace Pulumi
{
    public class PolicyForResource
    {
        public readonly PolicyPackResourceAttribute Annotation;
        public readonly MethodBase Target;
        public readonly string Type;
        public readonly Type ResourceClass;

        public PolicyForResource(PolicyPackResourceAttribute annotation,
            MethodBase target,
            String type,
            Type resourceClass)
        {
            this.Annotation = annotation;
            this.Target = target;
            this.Type = type;
            this.ResourceClass = resourceClass;
        }
    }

    public class PolicyForStack
    {
        public readonly PolicyPackStackAttribute Annotation;
        public readonly MethodBase Target;

        public PolicyForStack(PolicyPackStackAttribute annotation,
            MethodBase target)
        {
            this.Annotation = annotation;
            this.Target = target;
        }
    }

    public class PolicyPack
    {
        public readonly PolicyPackTypeAttribute Annotation;
        public readonly PolicyForStack? StackPolicy;
        public readonly ImmutableDictionary<String, PolicyForResource> ResourcePolicies;

        public PolicyPack(PolicyPackTypeAttribute annotation,
            PolicyForStack? stackPolicy,
            List<PolicyForResource> resourcePolicies)
        {
            var map = new Dictionary<string, PolicyForResource>();
            foreach (var policy in resourcePolicies)
            {
                map[policy.Type] = policy;
            }

            this.Annotation = annotation;
            this.StackPolicy = stackPolicy;
            this.ResourcePolicies = map.ToImmutableDictionary();
        }
    }

    public static class PolicyPackages
    {
        private static readonly Lazy<ImmutableList<PolicyPack>> _policies = new(DiscoverPolicies);

        public static ImmutableList<PolicyPack> Get()
        {
            return _policies.Value;
        }

        private static ImmutableList<PolicyPack> DiscoverPolicies()
        {
            var pairs =
                from a in ResourcePackages.DiscoverCandidateAssemblies()
                from t in a.GetTypes()
                let attr = t.GetCustomAttribute<PolicyPackTypeAttribute>()
                where attr != null
                select (t, attr);

            var res = new List<PolicyPack>();

            foreach (var pair in pairs)
            {
                var type = pair.t;
                var attr = pair.attr;

                PolicyForStack? stackPolicy = null;
                List<PolicyForResource> resourcePolicies = new();

                foreach (var m in type.GetMethods())
                {
                    var annotationResource = m.GetCustomAttribute<PolicyPackResourceAttribute>();
                    if (annotationResource != null)
                    {
                        if (!m.IsStatic)
                        {
                            throw new ArgumentException($"Method '{m}' of class '{type}': it should be static");
                        }

                        var types = m.GetParameters().Select(p => p.ParameterType).ToImmutableList();
                        if (types.Count != 2)
                        {
                            throw new ArgumentException($"Method '{m}' of class '{type}': it should have two parameters, a PolicyManager and a PolicyResource");
                        }

                        var classForResource = annotationResource.Target;
                        var typeForManager = types[0];
                        var typeForResource = types[1];

                        if (!typeForManager.IsAssignableFrom(typeof(PolicyManager)))
                        {
                            throw new ArgumentException($"Method '{m}' of class '{type}': first parameter has to be PolicyManager");
                        }

                        var annotation = classForResource.GetCustomAttribute<PolicyResourceTypeAttribute>();
                        if (annotation == null || classForResource != typeForResource)
                        {
                            throw new ArgumentException($"Method '{m}' of class '{type}': second parameter has to be a subclass of Pulumi PolicyResource");
                        }

                        resourcePolicies.Add(new PolicyForResource(annotationResource, m, annotation.Type, classForResource));
                    }

                    var annotationStack = m.GetCustomAttribute<PolicyPackStackAttribute>();
                    if (annotationStack != null)
                    {
                        if (!m.IsStatic)
                        {
                            throw new ArgumentException($"Method '{m}' of class '{type}': it should be static");
                        }

                        var types = m.GetParameters().Select(p => p.ParameterType).ToImmutableList();
                        if (types.Count() != 2)
                        {
                            throw new ArgumentException(
                                $"Method '{m}' of class '{type}': it should have two parameters, a PolicyManager and a list of PolicyResource");
                        }

                        var typeForManager = types[0];
                        var typeForResources = types[1];

                        if (!typeForManager.IsAssignableFrom(typeof(PolicyManager)))
                        {
                            throw new ArgumentException($"Method '{m}' of class '{type}': first parameter has to be PolicyManager");
                        }

                        if (!typeForResources.IsAssignableFrom(typeof(List<PolicyResource>)))
                        {
                            throw new ArgumentException($"Method '{m}' of class '{type}': second parameter has to be List<PolicyResource>");
                        }

                        if (stackPolicy != null)
                        {
                            throw new ArgumentException($"Multiple methods of class '{type}' declared as stack policies: {stackPolicy.Target} and {m}");
                        }

                        stackPolicy = new PolicyForStack(annotationStack, m);
                    }
                }

                if (stackPolicy != null || resourcePolicies.Count > 0)
                {
                    res.Add(new PolicyPack(pair.attr, stackPolicy, resourcePolicies));
                }
            }

            return res.ToImmutableList();
        }
    }
}

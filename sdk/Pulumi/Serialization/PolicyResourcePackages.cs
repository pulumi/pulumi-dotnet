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
        public PolicyPackResourceAttribute Annotation { get; }
        public MethodBase Target { get; }
        public string Type { get; }
        public Type ResourceClass { get; }

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

#pragma warning disable CA1711
    public class PolicyForStack
    {
        public PolicyPackStackAttribute Annotation { get; }
        public MethodBase Target { get; }

        public PolicyForStack(PolicyPackStackAttribute annotation,
            MethodBase target)
        {
            this.Annotation = annotation;
            this.Target = target;
        }
    }
#pragma warning restore CA1711

    public class PolicyPack
    {
        public PolicyPackTypeAttribute Annotation { get; }
        public PolicyForStack? StackPolicy { get; }
        public ImmutableDictionary<String, PolicyForResource> ResourcePolicyInputs { get; }
        public ImmutableDictionary<String, PolicyForResource> ResourcePolicyOutputs { get; }

        public PolicyPack(PolicyPackTypeAttribute annotation,
            PolicyForStack? stackPolicy,
            List<PolicyForResource> resourcePolicyInputs,
            List<PolicyForResource> resourcePolicyOutputs)
        {
            var mapInputs = new Dictionary<string, PolicyForResource>();
            foreach (var policy in resourcePolicyInputs)
            {
                mapInputs[policy.Type] = policy;
            }

            var mapOutputs = new Dictionary<string, PolicyForResource>();
            foreach (var policy in resourcePolicyOutputs)
            {
                mapOutputs[policy.Type] = policy;
            }

            this.Annotation = annotation;
            this.StackPolicy = stackPolicy;
            this.ResourcePolicyInputs = mapInputs.ToImmutableDictionary();
            this.ResourcePolicyOutputs = mapOutputs.ToImmutableDictionary();
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
                List<PolicyForResource> resourcePolicyInputs = new();
                List<PolicyForResource> resourcePolicyOutputs = new();

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

                        var typeForManager = types[0];
                        var typeForResource = types[1];

                        if (!typeForManager.IsAssignableFrom(typeof(PolicyManager)))
                        {
                            throw new ArgumentException($"Method '{m}' of class '{type}': first parameter has to be PolicyManager");
                        }

                        var annotation = typeForResource.GetCustomAttribute<PolicyResourceTypeAttribute>();
                        if (annotation == null)
                        {
                            throw new ArgumentException($"Method '{m}' of class '{type}': second parameter has to be a subclass of Pulumi PolicyResource");
                        }

                        if (typeof(PolicyResourceInput).IsAssignableFrom(typeForResource))
                        {
                            resourcePolicyInputs.Add(new PolicyForResource(annotationResource, m, annotation.Type, typeForResource));
                        }
                        else if (typeof(PolicyResourceOutput).IsAssignableFrom(typeForResource))
                        {
                            resourcePolicyOutputs.Add(new PolicyForResource(annotationResource, m, annotation.Type, typeForResource));
                        }
                        else
                        {
                            throw new ArgumentException($"Method '{m}' of class '{type}': second parameter has to be a subclass of Pulumi PolicyResource");
                        }
                    }

                    var annotationStack = m.GetCustomAttribute<PolicyPackStackAttribute>();
                    if (annotationStack != null)
                    {
                        if (!m.IsStatic)
                        {
                            throw new ArgumentException($"Method '{m}' of class '{type}': it should be static");
                        }

                        var types = m.GetParameters().Select(p => p.ParameterType).ToImmutableList();
                        if (types.Count != 2)
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

                        if (!typeForResources.IsAssignableFrom(typeof(List<PolicyResourceOutput>)))
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

                if (stackPolicy != null || resourcePolicyInputs.Count > 0 || resourcePolicyOutputs.Count > 0)
                {
                    res.Add(new PolicyPack(pair.attr, stackPolicy, resourcePolicyInputs, resourcePolicyOutputs));
                }
            }

            return res.ToImmutableList();
        }
    }
}

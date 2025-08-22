// Copyright 2025, Pulumi Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Pulumi.Experimental;

namespace Pulumi.Experimental.Policy
{
    // Represents a Pulumi resource as seen by an analyzer provider.
    public class AnalyzerProviderResource
    {
        public string Type { get; set; } = "";
        public ImmutableDictionary<string, PropertyValue> Properties { get; set; } = ImmutableDictionary<string, PropertyValue>.Empty;
        public string URN { get; set; } = "";
        public string Name { get; set; } = "";
    }

    // Defines the view of a Pulumi-managed resource as sent to Analyzers.
    public class AnalyzerResource
    {
        public string Type { get; set; } = "";
        public ImmutableDictionary<string, PropertyValue> Properties { get; set; } = ImmutableDictionary<string, PropertyValue>.Empty;
        public string URN { get; set; } = "";
        public string Name { get; set; } = "";
        public ResourceOptions? Options { get; set; }
        public AnalyzerProviderResource? Provider { get; set; }
        public string? Parent { get; set; }
        public List<string> Dependencies { get; set; } = new List<string>();
        public Dictionary<string, List<string>> PropertyDependencies { get; set; } = new Dictionary<string, List<string>>();
    }

    // Arguments passed to a resource validation policy.
    public class ResourceValidationArgs
    {
        public IPolicyManager Manager { get; }
        public AnalyzerResource Resource { get; }
        public Dictionary<string, object?>? Config { get; }

        public ResourceValidationArgs(IPolicyManager manager, AnalyzerResource resource, Dictionary<string, object?>? config = null)
        {
            Manager = manager ?? throw new ArgumentNullException(nameof(manager));
            Resource = resource ?? throw new ArgumentNullException(nameof(resource));
            Config = config;
        }
    }

    // Arguments passed to a stack validation policy.
    public class StackValidationArgs
    {
        public IPolicyManager Manager { get; }
        public List<AnalyzerResource> Resources { get; }

        public StackValidationArgs(IPolicyManager manager, List<AnalyzerResource> resources)
        {
            Manager = manager ?? throw new ArgumentNullException(nameof(manager));
            Resources = resources ?? throw new ArgumentNullException(nameof(resources));
        }
    }

    /// <summary>
    /// Defines the enforcement level of a policy.
    /// </summary>
    public enum EnforcementLevel
    {
        /// <summary>
        /// Displayed to users, but does not block deployment.
        /// </summary>
        Advisory = Pulumirpc.EnforcementLevel.Advisory,
        /// <summary>
        /// Stops deployment, cannot be overridden.
        /// </summary>
        Mandatory = Pulumirpc.EnforcementLevel.Mandatory,
        /// <summary>
        /// Disabled policies do not run during a deployment.
        /// </summary>
        Disabled = Pulumirpc.EnforcementLevel.Disabled
    }

    // Policy interface.
    public abstract class Policy
    {
        public string Name { get; }
        public string Description { get; }
        public EnforcementLevel EnforcementLevel { get; }

        protected Policy(string name, string description, EnforcementLevel enforcementLevel)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            EnforcementLevel = enforcementLevel;
        }
    }

    // Arguments for creating a resource validation policy.
    public class ResourceValidationPolicyArgs
    {
        public string Description { get; set; } = "";
        public EnforcementLevel EnforcementLevel { get; set; } = EnforcementLevel.Advisory;
        public Func<ResourceValidationArgs, CancellationToken, Task>? ValidateResource { get; set; }
    }

    // Implementation of a resource validation policy.
    public class ResourceValidationPolicy : Policy
    {
        private readonly Func<ResourceValidationArgs, CancellationToken, Task>? _validateResource;

        public ResourceValidationPolicy(string name, ResourceValidationPolicyArgs args) :
            base(name, args.Description, args.EnforcementLevel)
        {
            _validateResource = args.ValidateResource;
        }

        public async Task ValidateAsync(ResourceValidationArgs args, CancellationToken cancellationToken = default)
        {
            if (_validateResource != null)
            {
                await _validateResource(args, cancellationToken);
            }
        }
    }
}

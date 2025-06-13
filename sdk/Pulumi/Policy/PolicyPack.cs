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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Pulumi.Experimental.Policy
{
    // Represents a handshake request from the engine to the analyzer.
    public class HandshakeRequest
    {
        // The "host" of the analyzer. This is the engine that is running the analyzer.
        public IEngine Host { get; }

        // A *root directory* where the analyzer's binary, PulumiPolicy.yaml, or other identifying source code is
        // located.
        public string? RootDirectory { get; }

        // A *program directory* in which the analyzer should execute.
        public string? ProgramDirectory { get; }

        public HandshakeRequest(IEngine host, string? rootDirectory = null, string? programDirectory = null)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            RootDirectory = rootDirectory;
            ProgramDirectory = programDirectory;
        }
    }

    // Represents a handshake response from the analyzer to the engine.
    public class HandshakeResponse
    {
        // Currently empty
    }

    // Provides configuration for a policy.
    public class PolicyConfig
    {
        public EnforcementLevel EnforcementLevel { get; set; }
        public Dictionary<string, object?> Properties { get; set; }

        public PolicyConfig(EnforcementLevel enforcementLevel, Dictionary<string, object?> properties)
        {
            EnforcementLevel = enforcementLevel;
            Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        }
    }

    // Provides configuration information to the analyzer.
    public class ConfigureRequest
    {
        public Dictionary<string, PolicyConfig>? PolicyConfig { get; set; }
    }


    // Implementation of PolicyPack
    public sealed partial class PolicyPack
    {
        private static readonly Regex PolicyPackNameRegex = new Regex(@"^[a-zA-Z0-9-_.]{1,100}$", RegexOptions.Compiled);

        public string Name { get; }
        public IReadOnlyList<Policy> Policies { get; }
        private readonly Func<HandshakeRequest, CancellationToken, Task<HandshakeResponse>>? _handshake;

        public PolicyPack(
            string name,
            IReadOnlyList<Policy> policies,
            Func<HandshakeRequest, CancellationToken, Task<HandshakeResponse>>? handshake = null)
        {
            if (string.IsNullOrEmpty(name) || !PolicyPackNameRegex.IsMatch(name))
                throw new ArgumentException($"Invalid policy pack name: \"{name}\"", nameof(name));

            foreach (var policy in policies)
            {
                if (policy.Name == "all")
                    throw new ArgumentException($"Invalid policy name \"{policy.Name}\". \"all\" is a reserved name.");
            }

            Name = name;
            Policies = policies;
            _handshake = handshake;
        }

        public async Task<HandshakeResponse> HandshakeAsync(HandshakeRequest request, CancellationToken cancellationToken = default)
        {
            if (_handshake != null)
            {
                return await _handshake(request, cancellationToken);
            }
            return new HandshakeResponse();
        }
    }
}

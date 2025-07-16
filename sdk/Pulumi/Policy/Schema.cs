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

using System.Collections.Generic;

namespace Pulumi.Experimental.Policy
{
    /// <summary>
    /// Represents the configuration schema for a policy.
    /// </summary>
    public class PolicyConfigSchema
    {
        /// <summary>
        /// The policy's configuration properties.
        /// </summary>
        public Dictionary<string, System.Text.Json.JsonElement>? Properties { get; set; }

        /// <summary>
        /// The configuration properties that are required.
        /// </summary>
        public List<string>? Required { get; set; }
    }
}

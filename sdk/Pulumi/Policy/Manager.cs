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

namespace Pulumi.Experimental.Policy
{
    /// <summary>
    /// An interface into the policy system for reporting policy violations.
    /// </summary>
    public interface IPolicyManager
    {
        /// <summary>
        /// ReportViolation reports a policy violation with the given message and optional URN (it can be left null or empty).
        /// </summary>
        void ReportViolation(string message, string? urn = null);
    }

    internal class PolicyManager : IPolicyManager
    {
        private readonly Action<string, string?> _reportViolation;

        public PolicyManager(Action<string, string?> reportViolation)
        {
            _reportViolation = reportViolation;
        }

        public void ReportViolation(string message, string? urn = null)
        {
            _reportViolation(message, urn);
        }
    }
}

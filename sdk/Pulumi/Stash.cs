// Copyright 2025-2025, Pulumi Corporation.
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
using Pulumi;

namespace Pulumi
{
    /// <summary>
    /// Stash stores an arbitrary value in the state.
    /// </summary>
    public sealed class Stash : CustomResource
    {
        /// <summary>
        /// The value saved in the state for the stash.
        /// </summary>
        [Output("output")]
        public Output<object> Output { get; private set; } = null!;

        /// <summary>
        /// The most recent value passed to the stash resource.
        /// </summary>
        [Output("input")]
        public Output<object> Input { get; private set; } = null!;

        /// <summary>
        /// Create a <see cref="Stash"/> resource with the given arguments and options.
        /// </summary>
        /// <param name="name">The unique name of the resource.</param>
        /// <param name="args">The arguments to use to populate this resource's properties.</param>
        /// <param name="options">A bag of options that control this resource's behavior.</param>
        public Stash(string name, StashArgs args, CustomResourceOptions? options = null)
            : base("pulumi:index:Stash", name, args, options)
        {
        }
    }

    /// <summary>
    /// The set of arguments for constructing a <see cref="Stash"/> resource.
    /// </summary>
    public sealed class StashArgs : ResourceArgs
    {
        /// <summary>
        /// The value to store in the stash resource.
        /// </summary>
        public Input<object> Input { get; set; } = null!;
    }
}

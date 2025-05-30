// *** WARNING: this file was generated by pulumi-language-dotnet. ***
// *** Do not edit by hand unless you're certain you know what you are doing! ***

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Pulumi.Serialization;

namespace Pulumi.Component
{
    /// <summary>
    /// A custom resource with a single string input and output
    /// </summary>
    [ComponentResourceType("component:index:Custom")]
    public partial class Custom : global::Pulumi.CustomResource
    {
        [Output("value")]
        public Output<string> Value { get; private set; } = null!;


        /// <summary>
        /// Create a Custom resource with the given unique name, arguments, and options.
        /// </summary>
        ///
        /// <param name="name">The unique name of the resource</param>
        /// <param name="args">The arguments used to populate this resource's properties</param>
        /// <param name="options">A bag of options that control this resource's behavior</param>
        public Custom(string name, CustomArgs args, CustomResourceOptions? options = null)
            : base("component:index:Custom", name, args ?? new CustomArgs(), MakeResourceOptions(options, ""))
        {
        }

        private Custom(string name, Input<string> id, CustomResourceOptions? options = null)
            : base("component:index:Custom", name, null, MakeResourceOptions(options, id))
        {
        }

        private static CustomResourceOptions MakeResourceOptions(CustomResourceOptions? options, Input<string>? id)
        {
            var defaultOptions = new CustomResourceOptions
            {
                Version = Utilities.Version,
            };
            var merged = CustomResourceOptions.Merge(defaultOptions, options);
            // Override the ID if one was specified for consistency with other language SDKs.
            merged.Id = id ?? merged.Id;
            return merged;
        }
        /// <summary>
        /// Get an existing Custom resource's state with the given name, ID, and optional extra
        /// properties used to qualify the lookup.
        /// </summary>
        ///
        /// <param name="name">The unique name of the resulting resource.</param>
        /// <param name="id">The unique provider ID of the resource to lookup.</param>
        /// <param name="options">A bag of options that control this resource's behavior</param>
        public static Custom Get(string name, Input<string> id, CustomResourceOptions? options = null)
        {
            return new Custom(name, id, options);
        }
    }

    public sealed class CustomArgs : global::Pulumi.ResourceArgs
    {
        [Input("value", required: true)]
        public Input<string> Value { get; set; } = null!;

        public CustomArgs()
        {
        }
        public static new CustomArgs Empty => new CustomArgs();
    }
}

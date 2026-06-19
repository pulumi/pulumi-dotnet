using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

namespace Components
{
    public class KeywordComponentArgs : global::Pulumi.ResourceArgs
    {
        /// <summary>
        /// An input passed to the component
        /// </summary>
        [Input("input")]
        public Input<bool> Input { get; set; } = null!;
    }

    public class KeywordComponent : global::Pulumi.ComponentResource
    {
        [Output("result")]
        public Output<bool> Result { get; private set; }
        public KeywordComponent(string name, KeywordComponentArgs args, ComponentResourceOptions? opts = null)
            : base("components:index:KeywordComponent", name, args, opts)
        {
            // A resource named `this` collides with the receiver pointer of the
            // ComponentResource class generated for this component. NodeJS must rename the
            // resource variable (e.g. to `_this`) while keeping the `parent: this` pointer
            // intact.
            var @this = new Simple.Resource($"{name}-this", new()
            {
                Value = args.Input,
            }, new CustomResourceOptions
            {
                Parent = this,
            });

            // Referencing `this` exercises that the rename is applied to references too, not
            // just the declaration. The name `parent` also overlaps with the `parent`
            // resource-option key, which must not be confused with this resource variable.
            var parent = new Simple.Resource($"{name}-parent", new()
            {
                Value = @this.Value,
            }, new CustomResourceOptions
            {
                Parent = this,
            });

            this.Result = parent.Value;

            this.RegisterOutputs(new Dictionary<string, object?>
            {
                ["result"] = parent.Value,
            });
        }
    }
}

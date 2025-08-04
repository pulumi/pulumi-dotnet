using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

namespace Components
{
    public class MyComponentArgs : global::Pulumi.ResourceArgs
    {
        /// <summary>
        /// A simple input
        /// </summary>
        [Input("input")]
        public Input<bool> Input { get; set; } = null!;
    }

    public class MyComponent : global::Pulumi.ComponentResource
    {
        [Output("output")]
        public Output<bool> Output { get; private set; }
        public MyComponent(string name, MyComponentArgs args, ComponentResourceOptions? opts = null)
            : base("components:index:MyComponent", name, args, opts)
        {
            var res = new Simple.Resource($"{name}-res", new()
            {
                Value = args.Input,
            }, new CustomResourceOptions
            {
                Parent = this,
            });

            this.Output = res.Value;

            this.RegisterOutputs(new Dictionary<string, object?>
            {
                ["output"] = res.Value,
            });
        }
    }
}

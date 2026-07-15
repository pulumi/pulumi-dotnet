using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Config_ = Pulumi.Config_;

namespace Components
{
    public class ProviderComponentArgs : global::Pulumi.ResourceArgs
    {
        [Input("text")]
        public Input<string> Text { get; set; } = null!;
    }

    public class ProviderComponent : global::Pulumi.ComponentResource
    {
        [Output("result")]
        public Output<string> Result { get; private set; }
        public ProviderComponent(string name, ProviderComponentArgs args, ComponentResourceOptions? opts = null)
            : base("components:index:ProviderComponent", name, args, opts)
        {
            var prov = new Config_.Provider($"{name}-prov", new()
            {
                Name = "my config",
            }, new CustomResourceOptions
            {
                Parent = this,
            });

            var res = new Config_.Resource($"{name}-res", new()
            {
                Text = args.Text,
            }, new CustomResourceOptions
            {
                Parent = this,
                Provider = prov,
            });

            this.Result = res.Text;

            this.RegisterOutputs(new Dictionary<string, object?>
            {
                ["result"] = res.Text,
            });
        }
    }
}

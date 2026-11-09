using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Primitive = Pulumi.Primitive;

namespace Components
{
    public class PrimitiveComponentArgs : global::Pulumi.ResourceArgs
    {
        [Input("numberArray")]
        public InputList<double> NumberArray { get; set; } = null!;
        [Input("booleanMap")]
        public InputMap<bool> BooleanMap { get; set; } = null!;
    }

    public class PrimitiveComponent : global::Pulumi.ComponentResource
    {
        public PrimitiveComponent(string name, PrimitiveComponentArgs args, ComponentResourceOptions? opts = null)
            : base("components:index:PrimitiveComponent", name, args, opts)
        {
            var res = new Primitive.Resource($"{name}-res", new()
            {
                Boolean = true,
                Float = 3.5,
                Integer = 3,
                String = "plain",
                NumberArray = args.NumberArray,
                BooleanMap = args.BooleanMap,
            }, new CustomResourceOptions
            {
                Parent = this,
            });

            this.RegisterOutputs();
        }
    }
}

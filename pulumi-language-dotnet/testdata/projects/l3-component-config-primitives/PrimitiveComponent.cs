using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Primitive = Pulumi.Primitive;

namespace Components
{
    public class PrimitiveComponentArgs : global::Pulumi.ResourceArgs
    {
        [Input("boolean")]
        public Input<bool> Boolean { get; set; } = null!;
        [Input("float")]
        public Input<double> Float { get; set; } = null!;
        [Input("integer")]
        public Input<int> Integer { get; set; } = null!;
        [Input("string")]
        public Input<string> String { get; set; } = null!;
    }

    public class PrimitiveComponent : global::Pulumi.ComponentResource
    {
        public PrimitiveComponent(string name, PrimitiveComponentArgs args, ComponentResourceOptions? opts = null)
            : base("components:index:PrimitiveComponent", name, args, opts)
        {
            var res = new Primitive.Resource($"{name}-res", new()
            {
                Boolean = args.Boolean,
                Float = args.Float,
                Integer = args.Integer,
                String = args.String,
                NumberArray = new[]
                {
                    -1.0,
                    0.0,
                    1.0,
                },
                BooleanMap = 
                {
                    { "t", true },
                    { "f", false },
                },
            }, new CustomResourceOptions
            {
                Parent = this,
            });

            this.RegisterOutputs();
        }
    }
}

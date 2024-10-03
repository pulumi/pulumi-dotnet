using System;
using System.Text;
using System.Threading.Tasks;
using Pulumi;

class ComponentArgs : ResourceArgs
{
}

class Component : ComponentResource
{
    public Component(string name, ComponentArgs args, ComponentResourceOptions? opts = null)
        : base("test:index:Test", name, args, opts)
    {
	    throw new InputPropertiesException(
	    "failing for a reason",
	    new[]
	    {
		new PropertyError("foo", "the failure reason"),
	    });
    }
}

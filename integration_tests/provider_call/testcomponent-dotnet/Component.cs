using Pulumi;

public sealed class ComponentArgs : ResourceArgs
{
}

class Component : ComponentResource
{
    public Component(string name, ComponentArgs args, ComponentResourceOptions? opts = null)
        : base("test:index:Test", name, args, opts)
    {
    }
}

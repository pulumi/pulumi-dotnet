using Pulumi;

class Component : ComponentResource
{
    public Component(string name, ComponentArgs args, ComponentResourceOptions? opts = null)
        : base("test:index:Test", name, args, opts)
    {
    }
}

class ComponentArgs : ResourceArgs
{
}

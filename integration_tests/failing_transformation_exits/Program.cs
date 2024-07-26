using Pulumi;
using System.Threading.Tasks;

class ComponentArgs : ResourceArgs { }

class Component : ComponentResource
{
    public Component(string name, ComponentArgs args, ComponentResourceOptions? options = null) : base("test:index:Component", name, args, options)
    {
    }
}

class MyStack : Stack
{
    static StackOptions Options() => new StackOptions()
    {
        ResourceTransformations = { FailingTransform() } 
    };

    public MyStack() : base(Options())
    {
        var component = new Component("test", new ComponentArgs());
    }

    static ResourceTransformation FailingTransform() 
    {
        return args => 
        {
            throw new System.Exception("Boom!");
            return null;
        };
    }
}

class Program 
{
    static Task<int> Main() => Deployment.RunAsync<MyStack>();
}
using Pulumi;


abstract class ComponentBase : ComponentResource
{
    protected ComponentBase(string type, string name, ResourceArgs? args, ComponentResourceOptions? options = null, bool remote = false)
        : base(type, name, args, options, remote)
    {
    }

    [Output("inheritOutputAttribute")]
    public abstract Output<string> InheritOutputAttribute { get; protected set; }
}

class Component : ComponentBase
{
    public Component(string name, ComponentArgs args, ComponentResourceOptions? opts = null)
        : base("test:index:Test", name, args, opts, remote: true)
    {
    }

    [Output("passwordResult")]
    public Output<string> PasswordResult { get; protected set; } = default!;

    [Output("complexResult")]
    public Output<ComplexType> ComplexResult { get; protected set; } = default!;

    // the Output attribute is inherited from the base class
    public override Output<string> InheritOutputAttribute { get; protected set; }
}

public abstract class ComponentArgsBase : ResourceArgs {
    [Input("inheritInputAttribute")]
    public abstract Input<string> InheritInputAttribute  { get; set; }
}

public sealed class ComponentArgs : ComponentArgsBase
{
    [Input("passwordLength")]
    public Input<int> PasswordLength { get; set; } = null!;

    [Input("complex")]
    public Input<ComplexTypeArgs> Complex { get; set; } = null!;

    // the Input attribute is inherited from the base class
    public override Input<string> InheritInputAttribute  { get; set; } = null!;
}

public abstract class ComplexTypeArgsBase : ResourceArgs {
    [Input("inheritOutputAttribute")]
    public abstract string InheritInputAttribute  { get; set; }
}

public sealed class ComplexTypeArgs : ComplexTypeArgsBase
{
    [Input("name", required: true)]
    public string Name { get; set; } = null!;

    [Input("intValue", required: true)]
    public int IntValue { get; set; }

    // the Input attribute is inherited from the base class
    public override string InheritInputAttribute  { get; set; } = null!;
}

public abstract class ComplexTypeBase
{
    [Output("inheritOutputAttribute")]
    public abstract string InheritOutputAttribute { get; set; }
}

[OutputType]
public sealed class ComplexType : ComplexTypeBase
{
    [Output("name")]
    public string Name { get; set; }

    [Output("intValue")]
    public int IntValue { get; set; }

    // the Output attribute is inherited from the base class
    public override string InheritOutputAttribute { get; set; }

    [OutputConstructor]
    public ComplexType(string name, int intValue, string inheritOutputAttribute)
    {
        Name = name;
        IntValue = intValue;
        InheritOutputAttribute = inheritOutputAttribute;
    }
}

using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var @class = new Simple.Resource("class", new()
    {
        Value = true,
    });

    var export = new Simple.Resource("export", new()
    {
        Value = true,
    });

    var mod = new Simple.Resource("mod", new()
    {
        Value = true,
    });

    var import = new Simple.Resource("import", new()
    {
        Value = true,
    });

    // TODO(pulumi/pulumi#18246): Pcl should support scoping based on resource type just like HCL does in TF so we can uncomment this.
    // output "import" {
    //   value = Resource["import"]
    // }
    var @object = new Simple.Resource("object", new()
    {
        Value = true,
    });

    var self = new Simple.Resource("self", new()
    {
        Value = true,
    });

    var @this = new Simple.Resource("this", new()
    {
        Value = true,
    });

    var @if = new Simple.Resource("if", new()
    {
        Value = true,
    });

    return new Dictionary<string, object?>
    {
        ["class"] = @class,
        ["export"] = export,
        ["mod"] = mod,
        ["object"] = @object,
        ["self"] = self,
        ["this"] = @this,
        ["if"] = @if,
    };
});


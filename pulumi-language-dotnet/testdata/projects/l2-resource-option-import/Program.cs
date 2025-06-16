using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var import = new Simple.Resource("import", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        ImportId = "fakeID123",
    });

    var notImport = new Simple.Resource("notImport", new()
    {
        Value = true,
    });

});


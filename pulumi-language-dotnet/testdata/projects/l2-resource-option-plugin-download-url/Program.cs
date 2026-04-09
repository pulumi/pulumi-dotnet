using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var withDefaultURL = new Simple.Resource("withDefaultURL", new()
    {
        Value = true,
    });

    var withExplicitDefaultURL = new Simple.Resource("withExplicitDefaultURL", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        PluginDownloadURL = "https://github.com/pulumi/pulumi-simple/releases/v${VERSION}",
    });

    var withCustomURL1 = new Simple.Resource("withCustomURL1", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        PluginDownloadURL = "https://custom.pulumi.test/provider1",
    });

    var withCustomURL2 = new Simple.Resource("withCustomURL2", new()
    {
        Value = false,
    }, new CustomResourceOptions
    {
        PluginDownloadURL = "https://custom.pulumi.test/provider2",
    });

});


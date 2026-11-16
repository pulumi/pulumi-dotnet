using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var createTimeout = config.Require("createTimeout");
    var noTimeouts = new Simple.Resource("noTimeouts", new()
    {
        Value = true,
    });

    var createOnly = new Simple.Resource("createOnly", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        CustomTimeouts = CustomTimeouts.FromStrings(create: "5m"),
    });

    var updateOnly = new Simple.Resource("updateOnly", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        CustomTimeouts = CustomTimeouts.FromStrings(update: "10m"),
    });

    var deleteOnly = new Simple.Resource("deleteOnly", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        CustomTimeouts = CustomTimeouts.FromStrings(delete: "3m"),
    });

    var readOnly = new Simple.Resource("readOnly", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        CustomTimeouts = CustomTimeouts.FromStrings(read: "9m"),
    });

    var allTimeouts = new Simple.Resource("allTimeouts", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        CustomTimeouts = CustomTimeouts.FromStrings(create: "2m", update: "4m", delete: "1m", read: "5m"),
    });

    var configTimeout = new Simple.Resource("configTimeout", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        CustomTimeouts = CustomTimeouts.FromStrings(create: createTimeout),
    });

});


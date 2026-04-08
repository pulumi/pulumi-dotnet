using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Nestedobject = Pulumi.Nestedobject;

return await Deployment.RunAsync(() => 
{
    var receiverIgnore = new Nestedobject.Receiver("receiverIgnore", new()
    {
        Details = new[]
        {
            new Nestedobject.Inputs.DetailArgs
            {
                Key = "a",
                Value = "b",
            },
        },
    }, new CustomResourceOptions
    {
        IgnoreChanges =
        {
            "details[0].key",
        },
    });

    var mapIgnore = new Nestedobject.MapContainer("mapIgnore", new()
    {
        Tags = 
        {
            { "env", "prod" },
        },
    }, new CustomResourceOptions
    {
        IgnoreChanges =
        {
            "tags[\"env\"]",
            "tags[\"with.dot\"]",
            "tags[\"with escaped \\\"\"]",
        },
    });

    var noIgnore = new Nestedobject.Target("noIgnore", new()
    {
        Name = "nothing",
    });

});


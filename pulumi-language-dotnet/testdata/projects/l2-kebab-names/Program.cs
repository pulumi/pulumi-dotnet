using System.Collections.Generic;
using System.Linq;
using Pulumi;
using KebabNames = Pulumi.KebabNames;

return await Deployment.RunAsync(() => 
{
    // The package name and module name are kebab-case. Resource and object type names cannot be
    // kebab-case yet (the metaschema forbids hyphens in the member segment of a token), and kebab-case
    // property names are not yet handled by all code generators.
    var first = new KebabNames.KebabModule.SomeResource("first", new()
    {
        TheInput = true,
        Nested = new KebabNames.KebabModule.Inputs.NestedInputArgs
        {
            NestedValue = "nested",
        },
    });

    var second = new KebabNames.KebabModule.AnotherResource("second", new()
    {
        TheInput = first.TheOutput.Apply(theOutput => theOutput.NestedOutput),
    });

});


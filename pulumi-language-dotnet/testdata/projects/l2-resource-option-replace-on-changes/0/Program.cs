using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Replaceonchanges = Pulumi.Replaceonchanges;

return await Deployment.RunAsync(() => 
{
    // Stage 0: Initial resource creation
    // Scenario 1: Schema-based replaceOnChanges on replaceProp
    var schemaReplace = new Replaceonchanges.ResourceA("schemaReplace", new()
    {
        Value = true,
        ReplaceProp = true,
    });

    // Scenario 2: Option-based replaceOnChanges on value
    var optionReplace = new Replaceonchanges.ResourceB("optionReplace", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        ReplaceOnChanges =
        {
            "value",
        },
    });

    // Scenario 3: Both schema and option - will change value
    var bothReplaceValue = new Replaceonchanges.ResourceA("bothReplaceValue", new()
    {
        Value = true,
        ReplaceProp = true,
    }, new CustomResourceOptions
    {
        ReplaceOnChanges =
        {
            "value",
        },
    });

    // Scenario 4: Both schema and option - will change replaceProp
    var bothReplaceProp = new Replaceonchanges.ResourceA("bothReplaceProp", new()
    {
        Value = true,
        ReplaceProp = true,
    }, new CustomResourceOptions
    {
        ReplaceOnChanges =
        {
            "value",
        },
    });

    // Scenario 5: No replaceOnChanges - baseline update
    var regularUpdate = new Replaceonchanges.ResourceB("regularUpdate", new()
    {
        Value = true,
    });

    // Scenario 6: replaceOnChanges set but no change
    var noChange = new Replaceonchanges.ResourceB("noChange", new()
    {
        Value = true,
    }, new CustomResourceOptions
    {
        ReplaceOnChanges =
        {
            "value",
        },
    });

    // Scenario 7: replaceOnChanges on value, but only replaceProp changes
    var wrongPropChange = new Replaceonchanges.ResourceA("wrongPropChange", new()
    {
        Value = true,
        ReplaceProp = true,
    }, new CustomResourceOptions
    {
        ReplaceOnChanges =
        {
            "value",
        },
    });

    // Scenario 8: Multiple properties in replaceOnChanges array
    var multiplePropReplace = new Replaceonchanges.ResourceA("multiplePropReplace", new()
    {
        Value = true,
        ReplaceProp = true,
    }, new CustomResourceOptions
    {
        ReplaceOnChanges =
        {
            "value",
            "replaceProp",
        },
    });

});


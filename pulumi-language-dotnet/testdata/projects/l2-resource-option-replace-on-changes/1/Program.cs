using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Replaceonchanges = Pulumi.Replaceonchanges;

return await Deployment.RunAsync(() => 
{
    // Stage 1: Change properties to trigger replacements
    // Scenario 1: Change replaceProp → REPLACE (schema triggers)
    var schemaReplace = new Replaceonchanges.ResourceA("schemaReplace", new()
    {
        Value = true,
        ReplaceProp = false,
    });

    // Changed from true
    // Scenario 2: Change value → REPLACE (option triggers)
    var optionReplace = new Replaceonchanges.ResourceB("optionReplace", new()
    {
        Value = false,
    }, new CustomResourceOptions
    {
        ReplaceOnChanges =
        {
            "value",
        },
    });

    // Scenario 3: Change value → REPLACE (option on value triggers)
    var bothReplaceValue = new Replaceonchanges.ResourceA("bothReplaceValue", new()
    {
        Value = false,
        ReplaceProp = true,
    }, new CustomResourceOptions
    {
        ReplaceOnChanges =
        {
            "value",
        },
    });

    // Scenario 4: Change replaceProp → REPLACE (schema on replaceProp triggers)
    var bothReplaceProp = new Replaceonchanges.ResourceA("bothReplaceProp", new()
    {
        Value = true,
        ReplaceProp = false,
    }, new CustomResourceOptions
    {
        ReplaceOnChanges =
        {
            "value",
        },
    });

    // Scenario 5: Change value → UPDATE (no replaceOnChanges)
    var regularUpdate = new Replaceonchanges.ResourceB("regularUpdate", new()
    {
        Value = false,
    });

    // Changed from true
    // Scenario 6: No change → SAME (no operation)
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

    // Scenario 7: Change replaceProp (not value) → UPDATE (marked property unchanged)
    var wrongPropChange = new Replaceonchanges.ResourceA("wrongPropChange", new()
    {
        Value = true,
        ReplaceProp = false,
    }, new CustomResourceOptions
    {
        ReplaceOnChanges =
        {
            "value",
        },
    });

    // Scenario 8: Change value → REPLACE (multiple properties marked)
    var multiplePropReplace = new Replaceonchanges.ResourceA("multiplePropReplace", new()
    {
        Value = false,
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


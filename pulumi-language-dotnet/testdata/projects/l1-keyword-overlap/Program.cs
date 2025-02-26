using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    // Keywords in various languages should be renamed and work.
    var @class = "class_output_string";

    var export = "export_output_string";

    var import = "import_output_string";

    var mod = "mod_output_string";

    var @object = 
    {
        { "object", "object_output_string" },
    };

    var self = "self_output_string";

    var @this = "this_output_string";

    var @if = "if_output_string";

    return new Dictionary<string, object?>
    {
        ["class"] = @class,
        ["export"] = export,
        ["import"] = import,
        ["mod"] = mod,
        ["object"] = @object,
        ["self"] = self,
        ["this"] = @this,
        ["if"] = @if,
    };
});


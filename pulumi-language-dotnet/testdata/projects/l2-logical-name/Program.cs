using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Simple = Pulumi.Simple;

return await Deployment.RunAsync(() => 
{
    var config = new Config();
    var configLexicalName = config.RequireBoolean("cC-Charlie_charlie.😃⁉️");
    var resourceLexicalName = new Simple.Resource("aA-Alpha_alpha.🤯⁉️", new()
    {
        Value = configLexicalName,
    });

    return new Dictionary<string, object?>
    {
        ["bB-Beta_beta.💜⁉"] = resourceLexicalName.Value,
        ["dD-Delta_delta.🔥⁉"] = resourceLexicalName.Value,
    };
});


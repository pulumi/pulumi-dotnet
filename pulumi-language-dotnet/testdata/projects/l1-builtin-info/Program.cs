using System.Collections.Generic;
using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() => 
{
    return new Dictionary<string, object?>
    {
        ["stackOutput"] = Deployment.Instance.StackName,
        ["projectOutput"] = Deployment.Instance.ProjectName,
        ["organizationOutput"] = Deployment.Instance.OrganizationName,
    };
});


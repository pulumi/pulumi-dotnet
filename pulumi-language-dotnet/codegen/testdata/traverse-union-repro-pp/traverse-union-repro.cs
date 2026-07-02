using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Infra = Pulumi.Infra;

return await Deployment.RunAsync(() => 
{
    var test = new Infra.FileSystem("test", new()
    {
        StorageCapacity = 64,
        SubnetIds = new[]
        {
            aws_subnet.Test1.Id,
        },
        DeploymentType = "SINGLE_AZ_1",
        ThroughputCapacity = 64,
    });

});


using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Sync = Pulumi.Sync;

return await Deployment.RunAsync(() => 
{
    var block_1 = new Sync.Block("block-1");

    var block_2 = new Sync.Block("block-2");

    var block_3 = new Sync.Block("block-3");

});


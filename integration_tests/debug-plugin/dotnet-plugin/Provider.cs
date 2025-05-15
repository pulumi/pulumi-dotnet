using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulumi.Experimental.Provider;
using Google.Protobuf.WellKnownTypes; // For Struct, Value, Empty


namespace Pulumi.DebugProvider
{
    public class Provider : Pulumi.Experimental.Provider.Provider
    {
        public Provider()
        {
        }
		public override Task<GetSchemaResponse> GetSchema(GetSchemaRequest request, System.Threading.CancellationToken ct)
		{
			return Task.FromResult(new GetSchemaResponse
			{
				Schema = "{\"name\":\"debugplugin\",\"version\":\"0.0.1\",\"resources\":{\"debugplugin:index:MyDebugResource\":{}}}"
			});
		}
    }
}

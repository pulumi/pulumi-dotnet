using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Pulumi;
using Pulumi.Testing;

namespace Pulumi.Tests.Mocks.Aliases
{
    class AliasesMocks : IMocks
    {
        public Task<object> CallAsync(MockCallArgs args)
        {
            return Task.FromResult<object>(args);
        }

        public async Task<(string? id, object state)> NewResourceAsync(
            MockResourceArgs args)
        {
            await Task.Delay(0);
            return ("myID", new Dictionary<string, object>());
        }
    }
}

// Copyright 2016-2020, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulumirpc;

namespace Pulumi.Testing
{
    internal class MockEngine : Experimental.IEngine
    {
        public readonly List<string> Errors = new List<string>();

        public Task LogAsync(Experimental.LogRequest request)
        {
            if (request.Severity == Experimental.LogSeverity.Error)
            {
                lock (this.Errors)
                {
                    this.Errors.Add(request.Message);
                }
            }

            return Task.CompletedTask;
        }
    }
}

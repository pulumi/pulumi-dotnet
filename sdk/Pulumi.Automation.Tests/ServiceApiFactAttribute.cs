// Copyright 2016-2022, Pulumi Corporation

using System;
using Xunit;

namespace Pulumi.Automation.Tests
{
    /// <summary>
    /// Skip a test if PULUMI_ACCESS_TOKEN is not set.
    /// </summary>
    public sealed class ServiceApiFactAttribute : FactAttribute
    {
        public ServiceApiFactAttribute()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PULUMI_ACCESS_TOKEN")))
            {
                Skip = "PULUMI_ACCESS_TOKEN not set";
            }
        }
    }
}

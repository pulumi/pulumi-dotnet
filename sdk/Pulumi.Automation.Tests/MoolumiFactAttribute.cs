// Copyright 2016-2023, Pulumi Corporation

using System;
using Xunit;
using static Pulumi.Automation.Tests.Utility;

namespace Pulumi.Automation.Tests
{
    /// <summary>
    /// Skip a test if test org is not moolumi.
    /// </summary>
    public sealed class MoolumiFactAttribute : FactAttribute
    {
        public MoolumiFactAttribute()
        {
            var testOrg = GetTestOrg();
            if (testOrg != "moolumi")
            {
                Skip = "Test org is not moolumi";
            }
        }
    }
}

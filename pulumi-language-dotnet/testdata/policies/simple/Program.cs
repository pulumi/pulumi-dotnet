// Copyright 2025, Pulumi Corporation.  All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.Experimental.Policy;

class Program
{
    static Task Main(string[] args)
    {
        return PolicyPack.Serve(args, "1.0.0", host => new PolicyPack(
            "simple",
            [
                new ResourceValidationPolicy("truthiness", new ResourceValidationPolicyArgs
                {
                    Description = "Verifies properties are true",
                    EnforcementLevel = EnforcementLevel.Advisory,
                    ValidateResource = (args, ct) =>
                    {
                        if (args.Resource.Type != "simple:index:Resource")
                            return Task.CompletedTask;

                        if (args.Resource.Properties.TryGetValue("value", out var val) && val.TryGetBool(out var b) && b)
                        {
                            args.Manager.ReportViolation("This is a test warning");
                        }

                        return Task.CompletedTask;
                    }
                }),
                new ResourceValidationPolicy("falsiness", new ResourceValidationPolicyArgs
                {
                    Description = "Verifies properties are false",
                    EnforcementLevel = EnforcementLevel.Mandatory,
                    ValidateResource = (args, ct) =>
                    {
                        if (args.Resource.Type != "simple:index:Resource")
                            return Task.CompletedTask;

                        if (args.Resource.Properties.TryGetValue("value", out var val) && val.TryGetBool(out var b) && !b)
                        {
                            args.Manager.ReportViolation("This is a test error");
                        }

                        return Task.CompletedTask;
                    }
                })
            ]), CancellationToken.None);
    }
}

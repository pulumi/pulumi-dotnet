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
        return PolicyPack.Serve(args, "2.0.0", host => new PolicyPack(
            "config",
            [
                new ResourceValidationPolicy("allowed", new ResourceValidationPolicyArgs
                {
                    Description = "Verifies properties",
                    EnforcementLevel = EnforcementLevel.Mandatory,
                    ValidateResource = (args, ct) =>
                    {
                        if (args.Resource.Type != "simple:index:Resource")
                            return Task.CompletedTask;

                        var value = (bool)args.Config["value"];

                        if (args.Resource.Properties.TryGetValue("value", out var val) && val.TryGetBool(out var b) && b != value)
                        {
                            args.Manager.ReportViolation(string.Format("Property was {0}", b.ToString().ToLowerInvariant()));
                        }

                        return Task.CompletedTask;
                    }
                }),
            ]), CancellationToken.None);
    }
}

// Copyright 2016-2019, Pulumi Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Pulumi;

class Program
{
    static Task<int> Main(string[] args)
    {
        return Deployment.RunAsync(async () =>
        {
            var slug = $"{Deployment.Instance.OrganizationName}/{Deployment.Instance.ProjectName}/{Deployment.Instance.StackName}";
            var a = new StackReference(slug);

            var oldVal = (ImmutableArray<object>)await a.GetValueAsync("val");
            if (oldVal.Length != 2 || (string)oldVal[0] != "a" || (string)oldVal[1] != "b")
            {
                throw new Exception("Invalid result");
            }

            return new Dictionary<string, object>
            {
                { "val2", Output.CreateSecret(new[] { "a", "b" }) }
            };
        });
    }
}
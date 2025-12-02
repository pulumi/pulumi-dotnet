// Copyright 2016-2025, Pulumi Corporation.  All rights reserved.

using Pulumi;

public partial class FailsOnReplace : Pulumi.CustomResource
{
    public FailsOnReplace(string name, FailsOnReplaceArgs args, CustomResourceOptions? options = null)
        : base("testprovider:index:FailsOnReplace", name, args ?? new FailsOnReplaceArgs(), options)
    {
    }
}

public sealed class FailsOnReplaceArgs : Pulumi.ResourceArgs
{
    public FailsOnReplaceArgs()
    {
    }
}


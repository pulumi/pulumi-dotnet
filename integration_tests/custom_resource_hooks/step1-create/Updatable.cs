// Copyright 2025, Pulumi Corporation.  All rights reserved.

// Exposes the Updatable resource from the testprovider.

using System;
using Pulumi;

public partial class Updatable : Pulumi.CustomResource
{
    [Output("value")]
    public Output<string> Value { get; private set; } = null!;

    public Updatable(string name, UpdatableArgs args, CustomResourceOptions? options = null)
        : base("testprovider:index:Updatable", name, args ?? new UpdatableArgs(), options)
    {
    }
}

public sealed class UpdatableArgs : Pulumi.ResourceArgs
{
    [Input("value", required: true)]
    public Input<string> Value { get; set; } = null!;

    public UpdatableArgs()
    {
    }
}

public partial class UpdatableProvider : global::Pulumi.ProviderResource
{
    public UpdatableProvider(string name, UpdatableProviderArgs? args = null, CustomResourceOptions? options = null)
        : base("testprovider", name, args ?? new UpdatableProviderArgs(), options)
    {
    }
}

public sealed class UpdatableProviderArgs : global::Pulumi.ResourceArgs
{
    public UpdatableProviderArgs()
    {
    }
    public static new UpdatableProviderArgs Empty => new UpdatableProviderArgs();
}

// Copyright 2016-2021, Pulumi Corporation.  All rights reserved.

// Exposes the Random resource from the testprovider.

using System;
using Pulumi;

public partial class Random : Pulumi.CustomResource
{
	[Output("length")]
	public Output<int> Length { get; private set; } = null!;

	[Output("result")]
	public Output<string> Result { get; private set; } = null!;

	public Random(string name, RandomArgs args, CustomResourceOptions? options = null)
		: base("testprovider:index:Random", name, args ?? new RandomArgs(), options)
	{
	}
}

public sealed class RandomArgs : Pulumi.ResourceArgs
{
	[Input("length", required: true)]
	public Input<int> Length { get; set; } = null!;

	public RandomArgs()
	{
	}
}

/// <summary>
/// A Random resource that simulates the MakeResourceOptions pattern used by all generated Pulumi SDK
/// resources. The constructor merges caller-supplied options with a set of defaults via
/// <see cref="CustomResourceOptions.Merge"/>, which is the code path that previously dropped
/// <see cref="ResourceOptions.ResourceTransforms"/>.
/// </summary>
public partial class RandomWithMakeOptions : Pulumi.CustomResource
{
    public RandomWithMakeOptions(string name, RandomArgs args, CustomResourceOptions? options = null)
        : base("testprovider:index:Random", name, args, MakeResourceOptions(options, ""))
    {
    }

    private static CustomResourceOptions MakeResourceOptions(CustomResourceOptions? options, Input<string>? id)
    {
        var defaultOptions = new CustomResourceOptions();
        var merged = CustomResourceOptions.Merge(defaultOptions, options);
        merged.Id = id ?? merged.Id;
        return merged;
    }
}

public partial class RandomProvider : global::Pulumi.ProviderResource
{
	public RandomProvider(string name, RandomProviderArgs? args = null, CustomResourceOptions? options = null)
		: base("testprovider", name, args ?? new RandomProviderArgs(), options)
	{
	}
}

public sealed class RandomProviderArgs : global::Pulumi.ResourceArgs
{
	public RandomProviderArgs()
	{
	}
	public static new RandomProviderArgs Empty => new RandomProviderArgs();
}
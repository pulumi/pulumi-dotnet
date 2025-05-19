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

	public void Invoke(InvokeArgs args, InvokeOptions? options = null)
	{
		var invokeResult = global::Pulumi.Deployment.Instance.Invoke<MyInvokeResult>(
			"testprovider:index:returnArgs", args, options
		);

		invokeResult.Apply(res =>
		{
			if (res.Length != 11)
			{
				throw new System.Exception("This is not 11, it's " + res.Length);
			}

			if (res.Prefix != "hello")
			{
				throw new System.Exception("This is not hello, it's " + res.Prefix);
			}

			return res;
		});
	}

	public sealed class MyInvokeArgs : global::Pulumi.InvokeArgs
	{
		[Input("prefix", required: true)]
		public string Prefix { get; set; } = null!;

		[Input("length", required: true)]
		public int Length { get; set; } = 123;

		public MyInvokeArgs()
		{
		}
		public static new MyInvokeArgs Empty => new MyInvokeArgs();
	}

	[OutputType]
    public sealed class MyInvokeResult
    {
        public readonly string Prefix;
		public readonly int Length;

        [OutputConstructor]
        public MyInvokeResult(string prefix, int length)
        {
			this.Length = length;
			this.Prefix = prefix;
        }
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
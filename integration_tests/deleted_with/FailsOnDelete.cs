// Copyright 2016-2023, Pulumi Corporation.  All rights reserved.

// Exposes the FailsOnDelete resource from the testprovider.

using Pulumi;

public partial class FailsOnDelete : Pulumi.CustomResource
{
	public FailsOnDelete(string name, CustomResourceOptions? options = null)
		: base("testprovider:index:FailsOnDelete", name, ResourceArgs.Empty, options)
	{
	}
}

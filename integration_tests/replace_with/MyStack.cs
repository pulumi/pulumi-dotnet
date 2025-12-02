// Copyright 2016-2025, Pulumi Corporation.  All rights reserved.

using System.Collections.Immutable;
using Pulumi;

class MyStack : Stack
{
    public MyStack()
    {
        var rand1 = new Random("random1", new RandomArgs
        {
            Length = 10,
        });

        var rand2 = new Random("random2", new RandomArgs
        {
            Length = 10,
        });

        new FailsOnReplace("failsonreplace", new FailsOnReplaceArgs(), new CustomResourceOptions
        {
            ReplaceWith = new[] { rand1, rand2 },
        });
    }
}


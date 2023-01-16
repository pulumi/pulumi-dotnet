// Copyright 2016-2023, Pulumi Corporation.  All rights reserved.

using Pulumi;

class MyStack : Stack
{
    public MyStack()
    {
        var rand = new Random("random", new RandomArgs
        {
            Length = 10,
        });

        new FailsOnDelete("failsondelete", new() { DeletedWith = rand });
    }
}

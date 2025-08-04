using Pulumi;

class MyStack : Stack
{
    public MyStack()
    {
        var res1 = new Random("res1", new RandomArgs
        {
            Length = 10,
        });

        var res2 = new Random("res2", new RandomArgs
        {
            Length = 10,
        });
    }
}

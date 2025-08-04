using Pulumi;

class MyStack : Stack
{
    public MyStack()
    {
        var res1 = new Random("res1", new RandomArgs
        {
            Length = 10,
        });

        throw new System.Exception("This is a test error");

        var res2 = new Random("res2", new RandomArgs
        {
            Length = 10,
        });
    }
}

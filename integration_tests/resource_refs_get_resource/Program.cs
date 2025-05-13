// Copyright 2016-2022, Pulumi Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using Pulumi;

return await Deployment.RunAsync(() =>
{

   var child = new Child("mychild", new()
   {
      Message = "hello world!",
   });
   var container = new Container("mycontainer", new()
   {
      Child = child,
   });

   container.Urn.Apply(urn =>
   {
      var roundTrippedContainer = new Container("mycontainer", new(), new()
      {
            Urn = urn,
      });
      Output.Tuple(
          child.Urn,
          roundTrippedContainer.Child.Apply(c => c.Urn),
          roundTrippedContainer.Child.Apply(c => c.Message)
      ).Apply(t =>
      {
          var (expectedUrn, actualUrn, actualMessage) = t;
          if (expectedUrn != actualUrn)
          {
              throw new Exception($"Expected urn '${expectedUrn}' not equal to actual urn '${actualUrn}'.");
          }
          if ("hello world!" != actualMessage)
          {
              throw new Exception($"Expected message 'hello world!' not equal to actual message '${actualMessage}'.");
          }
          return t;
      });
      return urn;
   });
});

class ChildArgs : ResourceArgs
{
    [Input("message")]
    public string Message { get; set; } = null!;
}

[ResourceType("test:index:Child", "")]
class Child : ComponentResource
{
    [Output("message")]
    public Output<string> Message { get; private set; } = null!;

    public Child(string name, ChildArgs args, ComponentResourceOptions? opts = null)
        : base("test:index:Child", name, args, opts)
    {
        if (opts?.Urn is null)
        {
            Message = Output.Create(args.Message);
            RegisterOutputs(new Dictionary<string, object?>
            {
                { "message", args.Message },
            });
        }
    }
}

class ContainerArgs : ResourceArgs
{
    [Input("child")]
    public Child Child { get; set; } = null!;
}

[ResourceType("test:index:Container", "")]
class Container : ComponentResource
{
    [Output("child")]
    public Output<Child> Child { get; private set; } = null!;

    public Container(string name, ContainerArgs args, ComponentResourceOptions? opts = null)
        : base("test:index:Container", name, args, opts)
    {
        if (opts?.Urn is null)
        {
            Child = Output.Create(args.Child);
            RegisterOutputs(new Dictionary<string, object?>
            {
                { "child", args.Child },
            });
        }
    }
}

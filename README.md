# .NET Language Provider

A .NET language provider for Pulumi. Use your favorite .NET language to write Pulumi programs and deploy infrastructure to any cloud.

## Installing the [nuget](https://www.nuget.org/packages/Pulumi) package
```
dotnet add package Pulumi
```

## Example Pulumi program with .NET and C#

Here's a simple example of a Pulumi app written in C# that creates some simple
AWS resources:

```c#
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.Aws.S3;

return await Deployment.RunAsync(() =>
{
    // Create the bucket, and make it public.
    var bucket = new Bucket("media", new () 
    { 
        Acl = "public-read" 
    });

    // Add some content.
    var content = new BucketObject("basic-content", new ()
    {
        Acl = "public-read",
        Bucket = bucket.Id,
        ContentType = "text/plain; charset=utf8",
        Key = "hello.txt",
        Source = new StringAsset("Made with ❤, Pulumi, and .NET"),
    });

    // Return some values that will become the Outputs of the stack.
    return new Dictionary<string, object>
    {
        ["hello"] = "world",
        ["bucket-id"] = bucket.Id,
        ["content-id"] = content.Id,
        ["object-url"] = Output.Format($"http://{bucket.BucketDomainName}/{content.Key}"),
    };
});
```

Make a Pulumi.yaml file:

```
$ cat Pulumi.yaml

name: hello-dotnet
runtime: dotnet
```

Then, configure it:

```
$ pulumi stack init hello-dotnet
$ pulumi config set aws:region us-west-2
```

And finally, `pulumi preview` and `pulumi up` as you would any other Pulumi project.

## Development and Testing

### Prerequisites
- Dotnet [SDK v6+](https://dotnet.microsoft.com/download/dotnet/6.0) installed on your machine.
- Go 1.19 or later (for building and testing `pulumi-language-dotnet`)
- The Pulumi CLI (for running automation tests) 

Then you can run one of the following commands:

```bash
# Build the Pulumi SDK
dotnet run build-sdk

# Running tests for the Pulumi SDK
dotnet run test-sdk

# Running tests for the Pulumi Automation SDK
dotnet run test-automation-sdk

# Building the language plugin
dotnet run build-language-plugin

# Testing the language plugin
dotnet run test-language-plugin

# Sync proto files from pulumi/pulumi
dotnet run sync-proto-files

# List all integration tests
dotnet run list-integration-tests

# Run a specific integration test
dotnet run integration test <testName>

# Run all integration tests
dotnet run all-integration-tests
```

## Public API Changes

When making changes to the code you may get the following compilation
error:

```
error RS0016: Symbol XYZ' is not part of the declared API.
```

This indicates a change in public API. If you are developing a change
and this is intentional, add the new API elements to
`PublicAPI.Shipped.txt` corresponding to your project (some IDEs
will do this automatically for you, but manual additions are fine as
well).

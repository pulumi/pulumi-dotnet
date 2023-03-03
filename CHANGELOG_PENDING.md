### Improvements
  - [sdk] Lazily initialize all alias combinations for older Pulumi engines during `RegisterRequest` preparation, not when constructing resources. Re-enable tests for `AllAliases` [#97](https://github.com/pulumi/pulumi-dotnet/pull/97)

  - [sdk/providers] Updated names of "Olds" and "News" to make it clear if they are old/new inputs or state. Also removed the GetPluginInfo overload, version should now be passed into the main Serve method (defaults to the assembly version).
    [#99](https://github.com/pulumi/pulumi-dotnet/pull/99)

  - [sdk] Added `StackReference.GetOutputDetailsAsync` to retrieve output values from stack references directly.
    [#103](https://github.com/pulumi/pulumi-dotnet/pull/103)

  - [sdk] When an exception is thrown from the constructor of a `Stack` subclass, prevent `TargetInvocationException` from obscuring the error message.
    [#106](https://github.com/pulumi/pulumi-dotnet/pull/106)

### Bug Fixes

### Improvements

  - [sdk] When an exception is thrown from the constructor of a `Stack` subclass, prevent `TargetInvocationException` from obscuring the error message.
    [#106](https://github.com/pulumi/pulumi-dotnet/pull/106)

  - [sdk/auto] Added additional fields to `WhoAmIResult` for URL and organizations.
    [#120](https://github.com/pulumi/pulumi-dotnet/pull/120)

  - [sdk/auto] Expose additional Pulumi refresh options to the Automation API.
    [#117](https://github.com/pulumi/pulumi-dotnet/pull/117)

### Bug Fixes
  - [sdk] Fix JSON serialisation of Input<T> types.
    [#112](https://github.com/pulumi/pulumi-dotnet/pull/112)
	
  - [sdk] Improve the error message from not implemented provider methods.
    [#125](https://github.com/pulumi/pulumi-dotnet/pull/125)
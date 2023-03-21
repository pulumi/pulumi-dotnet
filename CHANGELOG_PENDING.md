### Improvements

  - [sdk] When an exception is thrown from the constructor of a `Stack` subclass, prevent `TargetInvocationException` from obscuring the error message.
    [#106](https://github.com/pulumi/pulumi-dotnet/pull/106)

  - [sdk/auto] Added additional fields to `WhoAmIResult` for URL and organizations.
    [#120](https://github.com/pulumi/pulumi-dotnet/pull/120)

### Bug Fixes
  - [sdk] Fix JSON serialisation of Input<T> types.
    [#112](https://github.com/pulumi/pulumi-dotnet/pull/112)
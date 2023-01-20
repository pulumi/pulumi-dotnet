# 3.52.1 (2023-01-20)

### Improvements

- [sdk] Delegates alias computation to engine [#14](https://github.com/pulumi/pulumi-dotnet/issues/14)

### Bug Fixes

- [sdk] Work around a port parsing bug in the engine when using providers.
  [#82](https://github.com/pulumi/pulumi-dotnet/pull/82)

- [sdk] Rename "ID" properties to "Id" in the provider interfaces.
  [#84](https://github.com/pulumi/pulumi-dotnet/pull/84)

- [sdk] Fix a mixup of Urn and Id in the provider interface.
  [#83](https://github.com/pulumi/pulumi-dotnet/pull/83)


# 3.52.0 (2023-01-17)

### Improvements

- [sdk] Add experimental support for writing custom resource providers. This is a preview release, code
  documentation and test coverage is known to be minimal, and all APIs are subject to change. However it is
  complete enough to try out, and we hope to get feedback on the interface to refine and stabilize this
  shortly.
  [#76](https://github.com/pulumi/pulumi-dotnet/pull/76)

### Bug Fixes

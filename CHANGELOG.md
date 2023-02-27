# 3.54.1 (2023-02-27)

# 3.54.0 (2023-02-14)

### Improvements
  - [sdk] Lazily initialize all alias combinations for older Pulumi engines during `RegisterRequest` preparation, not when constructing resources. Re-enable tests for `AllAliases` [#97](https://github.com/pulumi/pulumi-dotnet/pull/97)

  - [sdk/providers] Updated names of "Olds" and "News" to make it clear if they are old/new inputs or state. Also removed the GetPluginInfo overload, version should now be passed into the main Serve method (defaults to the assembly version).
    [#99](https://github.com/pulumi/pulumi-dotnet/pull/99)

  - [sdk] Added `StackReference.GetOutputDetailsAsync` to retrieve output values from stack references directly.
    [#103](https://github.com/pulumi/pulumi-dotnet/pull/103)

# 3.53.0 (2023-01-27)

### Improvements

- [sdk/auto] Add stack tag methods to the automation API.
  [#89](https://github.com/pulumi/pulumi-dotnet/pull/89)

### Bug Fixes

- [sdk] Fix MockMonitor reporting DeletedWith wasn't supported.
  [#93](https://github.com/pulumi/pulumi-dotnet/pull/93)

- [sdk] Fix paket referencing Pulumi.
  [#91](https://github.com/pulumi/pulumi-dotnet/pull/91)

- [sdk] Correctly check for alias support in the engine and map fully specified alias urns.
  [#88](https://github.com/pulumi/pulumi-dotnet/pull/88)

- [sdk] Bring back the correct fallback behavior for calculating aliases for older Pulumi engines.
  [#94](https://github.com/pulumi/pulumi-dotnet/pull/94)

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

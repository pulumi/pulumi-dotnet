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
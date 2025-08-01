# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html),
and is generated by [Changie](https://github.com/miniscruff/changie).

## v3.86.0 - 2025-07-31

### Improvements

- [sdk] Allow setting resource hooks in transforms [#675](https://github.com/pulumi/pulumi-dotnet/pull/675)

## v3.85.1 - 2025-07-11

### Bug Fixes

- [sdk] Pick versioned artifacts when publishing [#667](https://github.com/pulumi/pulumi-dotnet/pull/667)

## v3.85.0 - 2025-07-11

### Improvements

- [sdk] Implement resource hooks in the .Net SDK [#663](https://github.com/pulumi/pulumi-dotnet/pull/663)

### Bug Fixes

- [sdk] Disable stack auto parenting for resources that are read from the engine [#615](https://github.com/pulumi/pulumi-dotnet/pull/615)

### Improvements

- [sdk/provider] Send old inputs to diff and update and delete [#650](https://github.com/pulumi/pulumi-dotnet/pull/650)

## v3.84.0 - 2025-06-18

### Improvements

- [sdk] Add support for invoke transforms [#606](https://github.com/pulumi/pulumi-dotnet/pull/606)

## v3.83.2 - 2025-06-12

### Bug Fixes

- [build] Fix release process [#643](https://github.com/pulumi/pulumi-dotnet/pull/643)

- [runtime] Respect grpc cancellation to stop running subcommands when the engine has requested it [#642](https://github.com/pulumi/pulumi-dotnet/pull/642)
## v3.83.1 - 2025-06-11

### Bug Fixes

- [build] Fix release process [#635](https://github.com/pulumi/pulumi-dotnet/pull/635)

## v3.83.0 - 2025-06-11

### Improvements

- [sdk] Move `URN` from the experimental providers namespace, to the core namespace [#619](https://github.com/pulumi/pulumi-dotnet/pull/619)

- [sdk/provider] Moved `Pulumi.Experimental.ProviderPulumi.Experimental.Provider.IHost` to `Pulumi.Experimental.IEngine` [#618](https://github.com/pulumi/pulumi-dotnet/pull/618)

- [sdk/provider] Move PropertyValue out of the Provider namespace [#623](https://github.com/pulumi/pulumi-dotnet/pull/623)

## v3.82.1 - 2025-05-28

### Bug Fixes

- [runtime] Fix gen-sdk fails if there are dangling references [#614](https://github.com/pulumi/pulumi-dotnet/pull/614)

## v3.82.0 - 2025-05-21

### Improvements

- [runtime] Enable debugging providers using --attach-debugger=plugins [#600](https://github.com/pulumi/pulumi-dotnet/pull/600)

## v3.81.0 - 2025-05-15

### Improvements

- [sdk] Update PropertyValue to track secretness and dependencies directly on PropertyValue [#591](https://github.com/pulumi/pulumi-dotnet/pull/591)

### Bug Fixes

- [sdk] Component output properties are no longer overwritten with `unknown` [#595](https://github.com/pulumi/pulumi-dotnet/pull/595)

### Improvements

- [sdk/auto] Expose `--remote-executor-*` flags in the Automation API [#587](https://github.com/pulumi/pulumi-dotnet/pull/587)

## v3.80.0 - 2025-05-07

### Improvements

- [sdk/auto] Add --run-program to destory and refresh operations [#580](https://github.com/pulumi/pulumi-dotnet/pull/580)

- [sdk/auto] Add `--exclude` and `--exclude-dependents` to the Automation API [#582](https://github.com/pulumi/pulumi-dotnet/pull/582)

## v3.79.0 - 2025-04-24

### Bug Fixes

- [sdk] Fix deserialising InputMap<T> with unknown values [#570](https://github.com/pulumi/pulumi-dotnet/pull/570)

### Improvements

- [sdk/auto] Adds the `ConfigFile` option to all operation options in the Automation API (UpOptions, PreviewOptions, RefreshOptions, DestroyOptions) to support specifyin [#573](https://github.com/pulumi/pulumi-dotnet/pull/573)

## v3.78.0 - 2025-04-08

### Bug Fixes

- [sdk] Async context is now captured from the main program and restored in transform functions [#561](https://github.com/pulumi/pulumi-dotnet/pull/561)

- [sdk] Use the invariant culture when converting strings/numbers, replace some readonly fields with readonly properties in `Pulumi.Experimental`, and rename some generic type parameters to follow the standard naming convention of being prefixed by `T`, e.g. `InputMap<V>` => `InputMap<TValue>` [#564](https://github.com/pulumi/pulumi-dotnet/pull/564)

### Improvements

- [sdk/auto] Add the `--preview-only` flag to the `destroy` command in the Automation API [#554](https://github.com/pulumi/pulumi-dotnet/pull/554)

- [sdk/provider] Replaced `PropertyValue.TryUnwrap` with `Unwrap` [#562](https://github.com/pulumi/pulumi-dotnet/pull/562)

### Bug Fixes

- [sdk/provider] Add missing support for EnumType to PropertyValueSerializer [#557](https://github.com/pulumi/pulumi-dotnet/pull/557)

## v3.77.0 - 2025-03-27

### Bug Fixes

- [sdk] Exclude resource references from property dependencies for packaged components [#488](https://github.com/pulumi/pulumi-dotnet/pull/488)

### Improvements

- [sdk/auto] Add --show-reads Support for Pulumi Up & Preview [#542](https://github.com/pulumi/pulumi-dotnet/pull/542)

- [sdk/provider] Infer package name and namespace in ComponentProviderHost [#555](https://github.com/pulumi/pulumi-dotnet/pull/555)

- [sdk/provider] Default the version of ComponentProviderHost to 0.0.0 [#556](https://github.com/pulumi/pulumi-dotnet/pull/556)

### Bug Fixes

- [sdk/provider] Handle logging arguments in provider's getEngineAddress [#536](https://github.com/pulumi/pulumi-dotnet/pull/536)

## v3.76.1 - 2025-03-10

## v3.76.0 - 2025-03-10

### Improvements

- [sdk] Make OutputConstructorAttribute optional [#438](https://github.com/pulumi/pulumi-dotnet/pull/438)

- [sdk] Log a warning when trying to convert outputs to strings [#525](https://github.com/pulumi/pulumi-dotnet/pull/525)

- [sdk/auto] Add `pulumi install` to Automation Api [#426](https://github.com/pulumi/pulumi-dotnet/pull/426)

- [sdk/auto] Add `--refresh` to preview, up and destroy commands [#431](https://github.com/pulumi/pulumi-dotnet/pull/431)

### Bug Fixes

- [sdk/converter] Fix conversion for nested Output<T> [#527](https://github.com/pulumi/pulumi-dotnet/pull/527)

## v3.75.2 - 2025-02-26

### Bug Fixes

- [runtime] Revert changes to target net8 in provider SDKs [#515](https://github.com/pulumi/pulumi-dotnet/pull/515)

## v3.75.1 - 2025-02-26

Follow up release after v3.75, this reverts the updates to targeting net8.

## v3.75.0 - 2025-02-26

### Improvements

- [sdk] updated pulumi submodule and go.mod sdk/pkg and excluded failing conformance tests [#482](https://github.com/pulumi/pulumi-dotnet/pull/482)

- [sdk] Support parameterization for remote component resources [#502](https://github.com/pulumi/pulumi-dotnet/pull/502)

### bug-fixes

- [sdk] Fix inconsistent behavior of inheritance for InputAttribute and OutputAttribute [#506](https://github.com/pulumi/pulumi-dotnet/pull/506)

### Improvements

- [sdk/auto] Add the `--preview-only` flag for the `refresh` command [#496](https://github.com/pulumi/pulumi-dotnet/pull/496)

- [sdk/provider] Schema Analyzer to infer component schemas from classes [#468](https://github.com/pulumi/pulumi-dotnet/pull/468)

- [sdk/provider] Implement component provider host for auto-inferred components [#507](https://github.com/pulumi/pulumi-dotnet/pull/507)

## v3.74.0 - 2025-02-19

### Improvements

- [sdk] Add ability to disable ToString on Output<T> [#461](https://github.com/pulumi/pulumi-dotnet/pull/461)

### bug-fixes

- [sdk] Handle null in InputMap/List implicit conversions [#459](https://github.com/pulumi/pulumi-dotnet/pull/459)

- [sdk] Fix the Provider and Providers option when used in resource transforms [#460](https://github.com/pulumi/pulumi-dotnet/pull/460)

- [sdk] Fix adding the same value to InputMap multiple times [#462](https://github.com/pulumi/pulumi-dotnet/pull/462)

## v3.73.0 - 2025-02-06

### Improvements

- [sdk] InputMap and InputList no longer flatten nested unknowns/secrets to apply to the whole object. [#449](https://github.com/pulumi/pulumi-dotnet/pull/449)

### Bug Fixes

- [runtime] Don't parse runtime options at startup, defer to the options sent for specific methods [#451](https://github.com/pulumi/pulumi-dotnet/pull/451)

## v3.72.0 - 2025-01-30

### Bug Fixes

- [sdk] Avoid calling invokes with dependencies on unknown resources [#441](https://github.com/pulumi/pulumi-dotnet/pull/441)

- [sdk] Wait for resources in the input property dependencies [#444](https://github.com/pulumi/pulumi-dotnet/pull/444)

### Improvements

- [runtime] Plugins with msbuild warnings can still be run [#437](https://github.com/pulumi/pulumi-dotnet/pull/437)

- [runtime] Implement GetRequiredPackages to replace GetRequiredPlugins [#440](https://github.com/pulumi/pulumi-dotnet/pull/440)

## v3.71.1 - 2024-12-19

### Bug Fixes

- [sdk] Await background tasks during inline deployment [#420](https://github.com/pulumi/pulumi-dotnet/pull/420)

- [sdk] Fix parameterized explicit providers [#435](https://github.com/pulumi/pulumi-dotnet/pull/435)

- [runtime] Fix the language plugin to return a version [#390](https://github.com/pulumi/pulumi-dotnet/pull/390)

## v3.71.0 - 2024-12-05

### Improvements

- [sdk] Allow specifying dependencies for output invokes [#412](https://github.com/pulumi/pulumi-dotnet/pull/412)

- [sdk/provider] Add Parameterize to the provider interface [#404](https://github.com/pulumi/pulumi-dotnet/pull/404)

## v3.70.0 - 2024-11-27

### Bug Fixes

- [sdk/auto] Fix warning for inline programs [#388](https://github.com/pulumi/pulumi-dotnet/pull/388)

### Improvements

- [runtime] Reduce binary size by stripping debug information [#411](https://github.com/pulumi/pulumi-dotnet/pull/411)

## v3.69.0 - 2024-11-21

### Improvements

- [sdk] Make Pulumi.RunException public [#364](https://github.com/pulumi/pulumi-dotnet/pull/364)

- [sdk] Add `DeferredOutput` for resolving some output/input cycles [#385](https://github.com/pulumi/pulumi-dotnet/pull/385)

### bug-fixes

- [sdk] Support input lists and maps in JsonSerializer.SerializeAsync and JsonSerializer.DeserializeAsync [#372](https://github.com/pulumi/pulumi-dotnet/pull/372)

- [sdk] Fix publishing to set a required property used by the Automation Api to Install Pulumi cli [#393](https://github.com/pulumi/pulumi-dotnet/pull/393)

### Improvements

- [sdk/auto] Lessen the strictness of `OperationTypeConverter` to allow unknown operations [#350](https://github.com/pulumi/pulumi-dotnet/pull/350)

- [sdk/auto] Update YamlDotNet to v16.1.2 [#354](https://github.com/pulumi/pulumi-dotnet/pull/354)

- [sdk/auto] Add pulumi stack change-secrets-provider to automation api [#383](https://github.com/pulumi/pulumi-dotnet/pull/383)

- [sdk/provider] OutputReference.Value will normalize to null for Computed values [#381](https://github.com/pulumi/pulumi-dotnet/pull/381)

### Bug Fixes

- [sdk/provider] Fix a bug deserialising unknown secrets [#378](https://github.com/pulumi/pulumi-dotnet/pull/378)

- [runtime] Improve the detections of project files when attaching a debugger [#255](https://github.com/pulumi/pulumi-dotnet/pull/255)

- [runtime] Fix RunPlugin with new versions of the pulumi cli [#395](https://github.com/pulumi/pulumi-dotnet/pull/395)

## v3.68.0 - 2024-09-17

### Improvements

- [sdk] Parameterized providers are now considered stable [#347](https://github.com/pulumi/pulumi-dotnet/pull/347)

- [sdk/provider] Support authoring multi-language components in .NET [#275](https://github.com/pulumi/pulumi-dotnet/pull/275)

## v3.67.1 - 2024-09-13

### Bug Fixes

- [runtime] Fix debugger support [#343](https://github.com/pulumi/pulumi-dotnet/pull/343)

## v3.67.0 - 2024-09-10

### Improvements

- [sdk] Add support for attaching debuggers [#332](https://github.com/pulumi/pulumi-dotnet/pull/332)

### Bug Fixes

- [sdk/provider] Fix serialization of ComponentResources (no id required) [#331](https://github.com/pulumi/pulumi-dotnet/pull/331)

- [sdk/provider] Fix output value serialization. [#337](https://github.com/pulumi/pulumi-dotnet/pull/337)

## v3.66.2 - 2024-08-20

### Bug Fixes

- [sdk] Update Pulumi.Protobuf to v3.27.3 (fork) [#324](https://github.com/pulumi/pulumi-dotnet/pull/324)

## v3.66.1 - 2024-08-09

### Bug Fixes

- [sdk] Fix binary compatibility with provider SDKs built using older version of the core SDK [#318](https://github.com/pulumi/pulumi-dotnet/pull/318)

## v3.66.0 - 2024-08-09

### Improvements

- [sdk] Support package parameterization for Read/RegisterResource/Call/Invoke [#311](https://github.com/pulumi/pulumi-dotnet/pull/311)

### Bug Fixes

- [sdk] Fix type annotations for inputListFromT0/1 [#301](https://github.com/pulumi/pulumi-dotnet/pull/301)

- [sdk] Fix race condition in GrpcMonitor's GrpcChannel management [#304](https://github.com/pulumi/pulumi-dotnet/pull/304)

- [sdk] Fix unknown inputs deserialization [#306](https://github.com/pulumi/pulumi-dotnet/pull/306)

- [sdk] Fix program hanging when a resource transformation throws an exception [#307](https://github.com/pulumi/pulumi-dotnet/pull/307)

- [sdk] Fix handling of input properties with backing fields [#308](https://github.com/pulumi/pulumi-dotnet/pull/308)

### Improvements

- [sdk/auto] Implement Stack.ImportAsync() for batch importing resources into a stack [#296](https://github.com/pulumi/pulumi-dotnet/pull/296)

## v3.65.0 - 2024-07-18

### Improvements

- [sdk] Update Grpc dependency. [#256](https://github.com/pulumi/pulumi-dotnet/pull/256)

- [sdk] Strongly type URN values in Provider [#293](https://github.com/pulumi/pulumi-dotnet/pull/293)

### Bug Fixes

- [sdk] Enable .net analyzers and fix warnings. [#278](https://github.com/pulumi/pulumi-dotnet/pull/278)

- [sdk] Bufix Parsing of CustomTimeouts [#290](https://github.com/pulumi/pulumi-dotnet/pull/290)

- [sdk] Add support for deserializing output values and use them from transforms [#298](https://github.com/pulumi/pulumi-dotnet/pull/298)

### Improvements

- [runtime] Update pulumi/pulumi to 3.121 [#288](https://github.com/pulumi/pulumi-dotnet/pull/288)

## v3.64.0 - 2024-06-10

### Improvements

- [sdk] Make transforms a stable feature, not experimental [#270](https://github.com/pulumi/pulumi-dotnet/pull/270)

- [sdk/provider] Refactor Provider tests in order to prepare integration testing [#277](https://github.com/pulumi/pulumi-dotnet/pull/277)

### Bug Fixes

- [runtime] Upgrade dependencies [#279](https://github.com/pulumi/pulumi-dotnet/pull/279)

## v3.63.1 - 2024-04-25

### Bug Fixes

- [sdk] Remove Google.Protobuf pinned dependency. [#268](https://github.com/pulumi/pulumi-dotnet/pull/268)

## v3.63.0 - 2024-04-25

### Improvements

- [sdk] Support the Result field for better support of up --continue-on-error [#259](https://github.com/pulumi/pulumi-dotnet/pull/259)

### Bug Fixes

- [sdk] Revert gRPC update that broke large messages. [#266](https://github.com/pulumi/pulumi-dotnet/pull/266)

### Improvements

- [sdk/auto] Add ContinueOnError option to the automation API [#265](https://github.com/pulumi/pulumi-dotnet/pull/265)

## v3.62.0 - 2024-04-22

### Improvements

- [sdk] Allow apply to have unknown values during updates [#258](https://github.com/pulumi/pulumi-dotnet/pull/258)

### Bug Fixes

- [sdk] Use InvariantCulture when parsing numbers from config [#262](https://github.com/pulumi/pulumi-dotnet/pull/262)

- [sdk] Pin Google.Protobuf to 3.24. [#263](https://github.com/pulumi/pulumi-dotnet/pull/263)

## v3.61.0 - 2024-04-16

### Improvements

- [sdk] Add attribute to handle deserialization of constructor parameters with name overrides [#231](https://github.com/pulumi/pulumi-dotnet/pull/231)

- [sdk] Add experimental support for the new transforms system [#234](https://github.com/pulumi/pulumi-dotnet/pull/234)

- [sdk] Add FSharp Ops helpers [#250](https://github.com/pulumi/pulumi-dotnet/pull/250)

- [sdk] Handle Outputs in derived Stacks [#251](https://github.com/pulumi/pulumi-dotnet/pull/251)

## v3.60.0 - 2024-03-05

### Improvements

- [sdk] Add environment add and remove commands to automation api [#210](https://github.com/pulumi/pulumi-dotnet/pull/210)

- [sdk] Update Grpc dependency. [#219](https://github.com/pulumi/pulumi-dotnet/pull/219)

- [sdk] Drop support for netcoreapp3.1 [#235](https://github.com/pulumi/pulumi-dotnet/pull/235)

- [sdk/auto] Add new API to install the Pulumi CLI from the Automation API [#226](https://github.com/pulumi/pulumi-dotnet/pull/226)

- [sdk/provider] Initial implementation of a reflection-based PropertyValue deserializer [#201](https://github.com/pulumi/pulumi-dotnet/pull/201)

## v3.59.0 - 2023-11-15

### Improvements

- [sdk] Implement reflection-based RegisterOutputs() for component resources [#200](https://github.com/pulumi/pulumi-dotnet/pull/200)

- [sdk] Support .NET 8. [#205](https://github.com/pulumi/pulumi-dotnet/pull/205)

### Bug Fixes

- [sdk/auto] Fix issue with specifying a git username for remote workspaces. [#186](https://github.com/pulumi/pulumi-dotnet/pull/186)

## v3.58.0 - 2023-10-27

### Bug Fixes

- [sdk] Register and await tasks created from `Apply` that don't return anything. [#183](https://github.com/pulumi/pulumi-dotnet/pull/183)

### Improvements

- [sdk/auto] Add support for the path option for config operations. [#191](https://github.com/pulumi/pulumi-dotnet/pull/191)

# 3.57.0 (2023-09-23)

### Improvements

- Converter SDK: add `Args: string[]` to the `ConvertProgramRequest` fields which allows converter plugins to access args provided to `pulumi convert` 
  [#181](https://github.com/pulumi/pulumi-dotnet/pull/181)

# 3.56.2 (2023-08-29)

### Improvements

- Plugin: clean up resources and exit cleanly on receiving SIGINT or CTRL_BREAK.

# 3.56.1 (2023-08-24)

### Bug Fixes
- [sdk/automation-api] Adds guards in a non-destructive way against invalid JSON engine event data.
  [#167](https://github.com/pulumi/pulumi-dotnet/pull/167)

# 3.56.0 (2023-08-09)

### Improvements
- [sdk] - Implements a `Converter` abstraction for building language converter plugins for Pulumi in dotnet. 

# 3.55.2 (2023-08-01)

### Bug Fixes

- [sdk] Fix the default version for dotnet providers.
  [#148](https://github.com/pulumi/pulumi-dotnet/pull/148)

# 3.55.1 (2023-05-26)

### Bug Fixes

- [sdk] Fix serialization secret JSON resource arguments.
  [#144](https://github.com/pulumi/pulumi-dotnet/pull/144)

# 3.55.0 (2023-05-24)

### Improvements

  - [sdk] When an exception is thrown from the constructor of a `Stack` subclass, prevent `TargetInvocationException` from obscuring the error message.
    [#106](https://github.com/pulumi/pulumi-dotnet/pull/106)

  - [sdk/auto] Added additional fields to `WhoAmIResult` for URL and organizations.
    [#120](https://github.com/pulumi/pulumi-dotnet/pull/120)

  - [sdk/auto] Expose additional Pulumi refresh options to the Automation API.
    [#117](https://github.com/pulumi/pulumi-dotnet/pull/117)

  - [sdk] Updated to the latest pulumi protobuf specification.
    [#135](https://github.com/pulumi/pulumi-dotnet/pull/135)

  - [sdk] Added `GetDouble` to `Config`.
    [#143](https://github.com/pulumi/pulumi-dotnet/pull/143)

### Bug Fixes

  - [sdk] Fix JSON serialisation of Input<T> types.
    [#112](https://github.com/pulumi/pulumi-dotnet/pull/112)

  - [sdk] Improve the error message from not implemented provider methods.
    [#125](https://github.com/pulumi/pulumi-dotnet/pull/125)

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

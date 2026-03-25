# Pulumi .NET SDK

The official .NET SDK for [Pulumi](https://www.pulumi.com), enabling cloud infrastructure management in C#, F#, and VB.NET. This repo contains three components: the C# SDK (NuGet packages), a Go language host (`pulumi-language-dotnet`), and Go-based integration/conformance tests.

## Start here
- `sdk/Pulumi/` — core SDK: resources, outputs, deployment engine, serialization
- `sdk/Pulumi.Automation/` — Automation API for programmatic Pulumi operations
- `pulumi-language-dotnet/main.go` — language host entry point (Go binary invoked by Pulumi CLI)
- `pulumi-language-dotnet/codegen/` — .NET code generation from Pulumi schemas
- `integration_tests/` — Go integration tests that exercise the SDK end-to-end
- `Makefile` — all dev commands

## Repository structure
| Directory | Contents |
|---|---|
| `sdk/Pulumi/` | Core SDK: resources, inputs/outputs, deployment, serialization, provider hosting |
| `sdk/Pulumi.Automation/` | Automation API (LocalWorkspace, stack operations) |
| `sdk/Pulumi.FSharp/` | F# convenience wrappers |
| `sdk/Pulumi.Tests/` | Unit tests for core SDK |
| `sdk/Pulumi.Automation.Tests/` | Unit tests for Automation API |
| `pulumi-language-dotnet/` | Go language host binary + codegen |
| `pulumi-language-dotnet/codegen/` | Code generation from Pulumi schemas to C# |
| `pulumi-language-dotnet/testdata/` | Conformance test fixtures and golden files |
| `integration_tests/` | Go integration tests (require Pulumi CLI + built language host) |
| `build/` | F# build helpers (legacy — prefer Makefile targets) |
| `.changes/` | Changie changelog fragments |
| `pulumi/` | Git submodule of pulumi/pulumi (pinned version for proto files and test infrastructure) |

## Tech stack
- **Protobuf**: gRPC service definitions live in `pulumi/proto/` (submodule). The C# SDK compiles `.proto` files via `Grpc.Tools` at build time. Uses `Pulumi.Protobuf` (a fork of `Google.Protobuf` with increased recursion limit)
- **mise**: Tool version manager — install with `mise install` to get correct Go, dotnet, gofumpt, golangci-lint, changie versions
- **changie**: Changelog management — every PR needs a changelog fragment

## Command canon
- Install tools: `mise install`
- Init submodule: `git submodule update --init --recursive`
- Build all: `make build`
- Build SDK only: `make build_sdk`
- Build language host only: `make build_language_host`
- Format all: `make format`
- Format check: `make format_check`
- Lint all: `make lint`
- Lint fix: `make lint_fix`
- Fast tests (SDK only): `make test_fast`
- SDK unit tests: `make test_sdk`
- Automation SDK tests: `make test_sdk_automation`
- Conformance tests: `make test_conformance`
- Codegen tests: `make test_codegen`
- Integration tests: `make test_integration`
- Full test suite: `make test`
- Single SDK test: `make test_sdk TEST_FILTER=TestName`
- Single integration test: `make test_integration TEST_FILTER=TestName`
- Add changelog entry: `make changelog` (runs `changie new`)
- Install language host: `make install`

## Code conventions

### C# conventions
- Nullable reference types are enabled globally (`<Nullable>enable</Nullable>`)
- Warnings are errors (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`)
- Line endings must be LF for `.cs` files (see `.editorconfig`)
- Formatting is enforced by `dotnet format` — no manual style configuration needed

### Go conventions
- Format with `gofumpt` (not `gofmt`)
- Lint with `golangci-lint` using `.golangci.yml` at repo root
- Tests use `gotestsum` as the runner

### Forbidden patterns
- Never edit files under `pulumi/` — it is a git submodule pointing to pulumi/pulumi
- Never edit `sdk/Pulumi/Pulumi.xml` by hand — it is generated during build
- Never commit `bin/` or `obj/` directories
- Never fabricate test output or skip running tests
- Never use `git push --force` or `git reset --hard` without explicit approval
- Never run `make install` in CI or without understanding it installs to your PULUMI_ROOT

## Architecture

### Two languages, one repo
The SDK is C# targeting `net6.0`. The language host and codegen are Go. Integration tests are Go programs that shell out to `pulumi` CLI with the locally-built language host. These are separate build systems that only connect at test time.

### Protobuf compilation
Proto files live in the `pulumi/` submodule under `pulumi/proto/`. The SDK's `.csproj` includes them via `<Protobuf>` items. The generated C# gRPC stubs are compiled at build time — there are no checked-in generated files for protos.

### SDK internal structure
- `Core/` — Input/Output types, the reactive programming model
- `Resources/` — Resource base classes, options, args, transforms, hooks
- `Deployment/` — The deployment engine: gRPC monitor/engine communication, resource registration
- `Serialization/` — Property marshaling between .NET objects and Pulumi values
- `Provider/` — Component provider hosting (for building custom providers in .NET)
- `Testing/` — Mock deployment infrastructure for unit testing Pulumi programs

### Codegen pipeline
`pulumi-language-dotnet/codegen/` generates C# code from Pulumi package schemas. Golden-file tests in `codegen/testdata/` validate output. Update golden files by running tests with `PULUMI_ACCEPT=1`.

## Escalate immediately if
- Tests fail after two debugging attempts
- A change requires modifying the `pulumi/` submodule
- Requirements are ambiguous or conflicting
- A change affects the gRPC protocol or proto definitions

## Generated code
| Generated file | Source | Regenerate |
|---|---|---|
| `sdk/Pulumi/Pulumi.xml` | XML doc comments in C# source | `make build_sdk` |
| gRPC C# stubs (in-memory at build) | `pulumi/proto/**/*.proto` | `make build_sdk` |
| Codegen golden files | `pulumi-language-dotnet/codegen/testdata/` | `PULUMI_ACCEPT=1 make test_codegen` |
| Conformance test snapshots | `pulumi-language-dotnet/testdata/` | `PULUMI_ACCEPT=1 make test_conformance` |

## If you change...
| What changed | Run |
|---|---|
| Any `.cs` file in `sdk/` | `make format_check && make lint_sdk && make test_fast` |
| Any `.go` file in `pulumi-language-dotnet/` | `make format_language_host_check && make lint_language_host && make test_codegen` |
| Codegen templates or logic | `make test_codegen` (update golden files with `PULUMI_ACCEPT=1` if intended) |
| Any `.go` file in `integration_tests/` | `make format_integration_tests_check && make lint_integration_tests && make test_integration TEST_FILTER=TestName` |
| `go.mod` or `go.sum` | Run `go mod tidy` in the affected directory |
| `.changie.yaml` or changelog fragments | `changie batch auto --dry-run` |
| Proto files (in submodule) | Escalate — do not change submodule content |

## Nested AGENTS.md files
- `pulumi-language-dotnet/AGENTS.md` — Go language host, codegen, golden file workflow, conformance tests

## Changelog
Every PR must include a changelog fragment. Run `make changelog` and select the component (`sdk`, `sdk/auto`, `sdk/provider`, `sdk/converter`, `runtime`) and kind (`Improvements` or `Bug Fixes`).

# pulumi-language-dotnet

Go language host for Pulumi .NET. Invoked by the Pulumi CLI to build, run, and generate code for .NET Pulumi programs.

## Key files

- `main.go` — entry point, gRPC language host server
- `language_test.go` — conformance tests against the Pulumi test framework
- `codegen/gen.go` — code generation from Pulumi schemas to C#
- `codegen/gen_program.go` — PCL to C# transpilation
- `codegen/testdata/` — golden files for codegen tests
- `testdata/` — conformance test fixtures (projects and SDKs)
- `version/` — version string injected at build time via `-ldflags`

## Commands (run from repo root)

- **Build:** `mise exec -- make build_language_host`
- **Lint:** `mise exec -- make lint_language_host`
- **Format:** `mise exec -- make format_language_host`
- **Conformance tests:** `mise exec -- make test_conformance`
- **Codegen tests:** `mise exec -- make test_codegen`
- **Single test:** `mise exec -- make test_conformance TEST_FILTER=TestName`

## Golden file workflow

Codegen and conformance tests use golden files. When you intentionally change output:
1. Run tests — they will fail showing the diff
2. Review the diff to confirm correctness
3. Re-run with `PULUMI_ACCEPT=1` to update: `PULUMI_ACCEPT=1 mise exec -- make test_codegen`
4. Commit the updated golden files

## Conformance test fixtures

- `testdata/projects/` — minimal Pulumi programs (one per test case, named `l1-*`, `l2-*`, `l3-*`)
- `testdata/sdks/` — fake SDK packages used by conformance tests
- `testdata/overrides/` — per-test file overrides applied on top of project templates

The `l1`/`l2`/`l3` prefixes indicate test complexity levels defined by the Pulumi conformance test framework in the `pulumi/` submodule.

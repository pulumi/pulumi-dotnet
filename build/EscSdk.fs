module EscSdk

// The ESC C# SDK (Pulumi.Esc.Sdk) lives natively in this repo under
// sdk/Pulumi.Esc.Sdk, like every other Pulumi C# package. It is generated from the
// OpenAPI spec, which is owned by github.com/pulumi/pulumi (sdk/esc/swagger.yaml).
//
// `dotnet run generate-esc-sdk` clones pulumi at the pinned ref, then runs
// openapi-generator with the C# templates under build/esc-codegen to (re)produce
// sdk/Pulumi.Esc.Sdk. The generated output is committed and built/published by the
// normal SDK pipeline; regenerate only when the spec changes.

open Fake.Core
open System.IO

let private pulumiRepo = "https://github.com/pulumi/pulumi"

// TODO(esc-merge): pin to the pulumi release tag once the ESC consolidation ships.
let pulumiRef = "merge-esc-into-monorepo"

/// Regenerate sdk/Pulumi.Esc.Sdk from the OpenAPI spec owned by pulumi/pulumi.
let generateEscSdk (repositoryRoot: string) =
    let codegenDir = Path.Combine(repositoryRoot, "build", "esc-codegen")
    let templates = Path.Combine(codegenDir, "templates", "csharp")
    let outDir = Path.Combine(repositoryRoot, "sdk", "Pulumi.Esc.Sdk")
    GitSync.cloneRepository $"--branch {pulumiRef} --depth 1 {pulumiRepo}" (fun clone ->
        let spec = Path.Combine(clone, "sdk", "esc", "swagger.yaml")
        // openapitools.json in codegenDir pins the generator to 7.6.0; run there so
        // the @openapitools/openapi-generator-cli wrapper picks it up.
        let props =
            "packageName=Pulumi.Esc.Sdk,targetFramework=net6.0,nullableReferenceTypes=true,"
            + "validatable=true,hideGenerationTimestamp=false,sourceFolder=."
        let args =
            $"--yes @openapitools/openapi-generator-cli generate -i \"{spec}\" -g csharp "
            + $"--library generichost --additional-properties {props} "
            + $"-t \"{templates}\" -o \"{outDir}\" --git-repo-id esc-sdk --git-user-id pulumi"
        if Shell.Exec("npx", args, codegenDir) <> 0 then
            failwith "openapi-generator failed to generate Pulumi.Esc.Sdk"
        // Conform the generated C# to the repo's formatting rules so `make
        // format_check` stays green after a regeneration.
        let sdkDir = Path.Combine(repositoryRoot, "sdk")
        if Shell.Exec("dotnet", "format Pulumi.Esc.Sdk/Pulumi.Esc.Sdk.csproj", sdkDir) <> 0 then
            failwith "dotnet format failed for Pulumi.Esc.Sdk")

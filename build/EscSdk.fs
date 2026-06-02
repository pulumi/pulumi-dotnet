module EscSdk

// Syncs the generated ESC C# SDK (Pulumi.Esc.Sdk) from the pulumi/pulumi monorepo
// into this repo, where it is built and published to NuGet alongside the other
// Pulumi packages. The generated source lives in pulumi/pulumi at
// sdk/esc/dotnet/Pulumi.Esc.Sdk (produced by `make generate-esc-sdk-csharp`).
//
// See: https://www.notion.so/Merging-ESC-into-the-Pulumi-monorepo (Decision 6).

let private pulumiRepo = "https://github.com/pulumi/pulumi"

// The pulumi/pulumi ref that contains the generated ESC C# SDK.
//
// TODO(esc-merge): pin this to the pulumi release tag once the monorepo merge
// ships. Until then we sync from the merge branch. This mirrors how
// PulumiSdkVersion pins the Go SDK version from pulumi-language-dotnet/go.mod.
let pulumiRef = "merge-esc-into-monorepo"

/// Clone pulumi/pulumi at the pinned ref and copy the generated Pulumi.Esc.Sdk
/// project into sdk/Pulumi.Esc.Sdk.
let syncEscSdk (repositoryRoot: string) =
    GitSync.repository {
        // git clone <options> <repo> <dir>; cloneRepository appends the temp dir.
        remoteRepository = $"--branch {pulumiRef} --depth 1 {pulumiRepo}"
        localRepositoryPath = repositoryRoot
        contents = [
            GitSync.folder {
                sourcePath = [ "sdk"; "esc"; "dotnet"; "Pulumi.Esc.Sdk" ]
                destinationPath = [ "sdk"; "Pulumi.Esc.Sdk" ]
            }
        ]
    }

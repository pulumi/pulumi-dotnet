﻿open System
open System.IO
open System.Linq
open Fake.IO
open Fake.Core
open Publish
open CliWrap
open CliWrap.Buffered

/// Recursively tries to find the parent of a file starting from a directory
let rec findParent (directory: string) (fileToFind: string) = 
    let path = if Directory.Exists(directory) then directory else Directory.GetParent(directory).FullName
    let files = Directory.GetFiles(path)
    if files.Any(fun file -> Path.GetFileName(file).ToLower() = fileToFind.ToLower()) 
    then path 
    else findParent (DirectoryInfo(path).Parent.FullName) fileToFind

let repositoryRoot = findParent __SOURCE_DIRECTORY__ "README.md";

let sdk = Path.Combine(repositoryRoot, "sdk")
let pulumiSdk = Path.Combine(sdk, "Pulumi")
let pulumiSdkTests = Path.Combine(sdk, "Pulumi.Tests")
let pulumiAutomationSdk = Path.Combine(sdk, "Pulumi.Automation")
let pulumiAutomationSdkTests = Path.Combine(sdk, "Pulumi.Automation.Tests")
let pulumiFSharp = Path.Combine(sdk, "Pulumi.FSharp")
let integrationTests = Path.Combine(repositoryRoot, "integration_tests")
let pulumiLanguageDotnet = Path.Combine(repositoryRoot, "pulumi-language-dotnet")

/// Runs `dotnet clean` command against the solution file,
/// then proceeds to delete the `bin` and `obj` directory of each project in the solution
let cleanSdk() = 
    let cmd = Cli.Wrap("dotnet").WithArguments("clean").WithWorkingDirectory(sdk)
    let output = cmd.ExecuteAsync().GetAwaiter().GetResult()
    if output.ExitCode <> 0 then
        failwith "Clean failed"

    let projects = [ 
        pulumiSdk
        pulumiSdkTests
        pulumiAutomationSdk
        pulumiAutomationSdkTests
        pulumiFSharp
    ]

    for project in projects do
        Shell.deleteDir (Path.Combine(project, "bin"))
        Shell.deleteDir (Path.Combine(project, "obj"))

/// Runs `dotnet restore` against the solution file without using cache
let restoreSdk() = 
    printfn "Restoring Pulumi SDK packages"
    if Shell.Exec("dotnet", "restore --no-cache", sdk) <> 0
    then failwith "restore failed"

/// Returns an array of names of go tests inside ./integration_tests
/// You can use this to see which tests are available,
/// then run individual tests using `dotnet run integration test <testName>`
let integrationTestNames() =
    let cmd = Cli.Wrap("go").WithArguments("test -list=.").WithWorkingDirectory(integrationTests)
    let output = cmd.ExecuteBufferedAsync().GetAwaiter().GetResult()
    if output.ExitCode <> 0 then
        failwith $"Failed to list go tests from {integrationTests}"
    
    output.StandardOutput.Split("\n")
    |> Array.filter (fun line -> line.StartsWith "Test")

let listIntegrationTests() =
    for testName in integrationTestNames() do
        printfn $"{testName}"

let buildSdk() = 
    cleanSdk()
    restoreSdk()
    printfn "Building Pulumi SDK"
    if Shell.Exec("dotnet", "build --configuration Release", sdk) <> 0
    then failwith "build failed"

/// Publishes packages for Pulumi, Pulumi.Automation and Pulumi.FSharp to nuget.
/// Requires NUGET_PUBLISH_KEY and PULUMI_VERSION environment variables.
/// When publishing, we check whether the package we are about to publish already exists on Nuget
/// and if that is the case, we skip it.
let publishSdks() =
    // prepare
    cleanSdk()
    restoreSdk()
    // perform the publishing (idempotent)
    let publishResults = publishSdks [
        pulumiSdk
        pulumiAutomationSdk
        pulumiFSharp
    ]
    
    match publishResults with
    | Error errorMsg -> printfn $"{errorMsg}"
    | Ok results ->
        for result in results do
            match result with
            | PublishResult.Ok project ->
                printfn $"Project '{projectName project}' has been published"
            | PublishResult.Failed(project, error) ->
                printfn $"Project '{projectName project}' failed to publish the nuget package: {error}"
            | PublishResult.AlreadyPublished project ->
                printfn $"Project '{projectName project}' has already been published"

        let anyProjectFailed = results |> List.exists (fun result -> result.HasErrored())
        if anyProjectFailed then
            let failedProjectsAtPublishing =
                results
                |> List.where (fun result -> result.HasErrored())
                |> List.map (fun result -> result.ProjectName())
            
            failwith $"Some nuget packages were not published: {failedProjectsAtPublishing}"

let cleanLanguagePlugin() = 
    let plugin = Path.Combine(pulumiLanguageDotnet, "pulumi-language-dotnet")
    if File.Exists plugin then File.Delete plugin

let buildLanguagePlugin() = 
    cleanLanguagePlugin()
    printfn "Building pulumi-language-dotnet Plugin"
    if Shell.Exec("go", "build", pulumiLanguageDotnet) <> 0
    then failwith "Building pulumi-language-dotnet failed"
    let output = Path.Combine(pulumiLanguageDotnet, "pulumi-language-dotnet")
    printfn $"Built binary {output}"

let testLanguagePlugin() = 
    cleanLanguagePlugin()
    printfn "Testing pulumi-language-dotnet Plugin"
    if Shell.Exec("go", "test", Path.Combine(repositoryRoot, "pulumi-language-dotnet")) <> 0
    then failwith "Testing pulumi-language-dotnet failed"

let testPulumiSdk() = 
    cleanSdk()
    restoreSdk()
    printfn "Testing Pulumi SDK"
    if Shell.Exec("dotnet", "test --configuration Release", pulumiSdkTests) <> 0
    then failwith "tests failed"

let testPulumiAutomationSdk() = 
    cleanSdk()
    restoreSdk()
    printfn "Testing Pulumi Automation SDK"
    if Shell.Exec("dotnet", "test --configuration Release", pulumiAutomationSdkTests) <> 0
    then failwith "automation tests failed"

let syncProtoFiles() = GitSync.repository {
    remoteRepository = "https://github.com/pulumi/pulumi.git"
    localRepositoryPath = repositoryRoot
    contents = [
        GitSync.folder {
            sourcePath = [ "proto"; "pulumi" ]
            destinationPath = [ "proto"; "pulumi" ]
        }
        
        GitSync.folder {
            sourcePath = [ "proto"; "google"; "protobuf" ]
            destinationPath = [ "proto"; "google"; "protobuf" ]
        }
    ]
}

let pulumiBinDirectory() =
    let homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
    let pulumiBin = Path.Combine(homeDir, ".pulumi-dev", "bin")
    pulumiBin

let installLanguagePluginLocally() = 
    cleanLanguagePlugin()
    buildLanguagePlugin()
    let pulumiBin = pulumiBinDirectory()
    Directory.ensure pulumiBin
    let builtPlugin = Path.Combine(repositoryRoot, "pulumi-language-dotnet", "pulumi-language-dotnet")
    let destination = Path.Combine(pulumiBin, "pulumi-language-dotnet")
    Shell.rm destination
    Shell.copyFile pulumiBin builtPlugin
    printfn $"Copied to {destination}"
    // update PATH environment variable with the location of ~/.pulumi-dev/bin
    // this is where we copied the language plugin.
    // This way, the Pulumi CLI will pick up this local plugin
    // instead of the one bundled with the CLI
    match env "PATH" with
    | None -> Environment.SetEnvironmentVariable("PATH", pulumiBin)
    | Some path -> Environment.SetEnvironmentVariable("PATH", $"{pulumiBin}:{path}")
    printfn $"Ensured that {pulumiBin} is first on the PATH"

let runSpecificIntegrationTest(testName: string) = 
    cleanSdk()
    installLanguagePluginLocally()
    if Shell.Exec("go", $"test -run=^{testName}$", Path.Combine(repositoryRoot, "integration_tests")) <> 0
    then failwith $"Integration test '{testName}' failed"

let runAllIntegrationTests() =
    installLanguagePluginLocally()
    for testName in integrationTestNames() do
        printfn $"Running test {testName}"
        cleanSdk()
        if Shell.Exec("go", $"test -run=^{testName}$", Path.Combine(repositoryRoot, "integration_tests")) <> 0
        then failwith $"Integration test '{testName}' failed"

[<EntryPoint>]
let main(args: string[]) : int = 
    match args with
    | [| "clean-sdk" |] -> cleanSdk()
    | [| "build-sdk" |] -> buildSdk()
    | [| "build-language-plugin" |] -> buildLanguagePlugin()
    | [| "test-language-plugin" |] -> testLanguagePlugin()
    | [| "install-language-plugin" |] -> installLanguagePluginLocally()
    | [| "test-sdk" |] -> testPulumiSdk()
    | [| "test-automation-sdk" |] -> testPulumiAutomationSdk()
    | [| "publish-sdks" |] -> publishSdks()
    | [| "sync-proto-files" |] -> syncProtoFiles()
    | [| "list-integration-tests" |] -> listIntegrationTests()
    | [| "integration"; "test"; testName |] -> runSpecificIntegrationTest testName
    | [| "all-integration-tests" |] -> runAllIntegrationTests()

    | otherwise -> printfn $"Unknown build arguments provided %A{otherwise}"

    0

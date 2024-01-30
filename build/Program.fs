open System
open System.IO
open System.Linq
open System.Text.RegularExpressions
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

let coverageDir = Path.Combine(repositoryRoot, "coverage")
let sdk = Path.Combine(repositoryRoot, "sdk")
let pulumiSdk = Path.Combine(sdk, "Pulumi")
let pulumiSdkTests = Path.Combine(sdk, "Pulumi.Tests")
let pulumiAutomationSdk = Path.Combine(sdk, "Pulumi.Automation")
let pulumiAutomationSdkTests = Path.Combine(sdk, "Pulumi.Automation.Tests")
let pulumiFSharp = Path.Combine(sdk, "Pulumi.FSharp")
let integrationTests = Path.Combine(repositoryRoot, "integration_tests")
let pulumiLanguageDotnet = Path.Combine(repositoryRoot, "pulumi-language-dotnet")

// Find the version of the Pulumi Go SDK that we are using for the language plugin.
let findGoSDKVersion =
    let goMod = Path.Combine(pulumiLanguageDotnet, "go.mod")
    try
        let lines = File.ReadAllLines(goMod)
        let patternRegex = new Regex("^\\s*github.com/pulumi/pulumi/sdk", RegexOptions.IgnoreCase)
        match Array.tryFind (patternRegex.IsMatch) lines with
        | Some(matchingLine) ->
            let version = matchingLine.Split(' ')[1]
            let version = version.TrimStart('v')
            Some(version)
        | None ->
            None    
    with
    | ex ->
        printfn "Error while trying to find the Go SDK version: %s" ex.Message

        None

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

/// Runs `dotnet format` against the solution file
let formatSdk verify =
    printfn "Formatting Pulumi SDK packages"
    let args = "format" + if verify then " --verify-no-changes" else ""
    if Shell.Exec("dotnet", args, sdk) <> 0
    then failwith "format failed"

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
    match findGoSDKVersion with
    | None -> failwith "Could not find the Pulumi SDK version in go.mod"
    | Some(version) ->
        printfn "Building Pulumi SDK"
        if Shell.Exec("dotnet", "build --configuration Release -p:PulumiSdkVersion=" + version, sdk) <> 0

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

let testPulumiSdk coverage =
    cleanSdk()
    restoreSdk()
    printfn "Testing Pulumi SDK"
    let coverageArgs = if coverage then $" -p:CollectCoverage=true -p:CoverletOutputFormat=cobertura -p:CoverletOutput={coverageDir}/coverage.pulumi.xml" else ""
    if Shell.Exec("dotnet", "test --configuration Release" + coverageArgs, pulumiSdkTests) <> 0
    then failwith "tests failed"

let testPulumiAutomationSdk coverage =
    cleanSdk()
    restoreSdk()
    match findGoSDKVersion with
    | None -> failwith "Could not find the Pulumi SDK version in go.mod"
    | Some(version) ->
        printfn "Testing Pulumi Automation SDK"
        let coverageArgs = if coverage then $" -p:CollectCoverage=true -p:CoverletOutputFormat=cobertura -p:CoverletOutput={coverageDir}/coverage.pulumi.automation.xml" else ""
        if Shell.Exec("dotnet", $"test --configuration Release -p:PulumiSdkVersion={version} {coverageArgs}", pulumiAutomationSdkTests) <> 0

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

let runSpecificIntegrationTest(testName: string) =
    buildLanguagePlugin()
    cleanSdk()
    if Shell.Exec("go", $"test -run=^{testName}$", Path.Combine(repositoryRoot, "integration_tests")) <> 0
    then failwith $"Integration test '{testName}' failed"

let runAllIntegrationTests() =
    buildLanguagePlugin()
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
    | [| "format-sdk" |] -> formatSdk false
    | [| "format-sdk"; "verify" |] -> formatSdk true
    | [| "build-language-plugin" |] -> buildLanguagePlugin()
    | [| "test-language-plugin" |] -> testLanguagePlugin()
    | [| "test-sdk" |] -> testPulumiSdk false
    | [| "test-sdk"; "coverage" |] -> testPulumiSdk true
    | [| "test-automation-sdk" |] -> testPulumiAutomationSdk false
    | [| "test-automation-sdk"; "coverage" |] -> testPulumiAutomationSdk true
    | [| "publish-sdks" |] -> publishSdks()
    | [| "sync-proto-files" |] -> syncProtoFiles()
    | [| "list-integration-tests" |] -> listIntegrationTests()
    | [| "integration"; "test"; testName |] -> runSpecificIntegrationTest testName
    | [| "all-integration-tests" |] -> runAllIntegrationTests()

    | otherwise -> printfn $"Unknown build arguments provided %A{otherwise}"

    0

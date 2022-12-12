module Publish

open System
open System.IO
open System.Linq
open Fake.IO
open Fake.Core

let [<Literal>] NUGET_PUBLISH_KEY = "NUGET_PUBLISH_KEY"
let [<Literal>] PULUMI_VERSION = "PULUMI_VERSION"

let env (variableName: string) =
    let value = Environment.GetEnvironmentVariable variableName
    if String.isNullOrWhiteSpace value
    then None
    else Some value

type PublishResult =
    { projectDir: string; success: bool; error: string }
    member this.ProjectName() = DirectoryInfo(this.projectDir).Name
    static member Failed(projectDir, error) = { projectDir = projectDir; success = false; error = error }
    static member Ok(projectDir) = { projectDir = projectDir; success = true; error = "" }

let publishSdk (projectDir: string) (version: string) (nugetApiKey: string) : PublishResult =
    let buildNugetCmd = String.concat " " [
        "build"
        "--configuration Release"
        $"-p:Version={version}"
    ]
    
    if Shell.Exec("dotnet", buildNugetCmd, projectDir) <> 0 then
        PublishResult.Failed(projectDir, "failed to build the nuget package")
    else
        let releaseDir = Path.Combine(projectDir, "bin", "Release")
        let releaseArtifacts = Directory.EnumerateFiles(releaseDir)
        if not (releaseArtifacts.Any()) then
            PublishResult.Failed(projectDir, "couldn't the nuget package")
        else
            let nugetPackageFile = releaseArtifacts.First()
            let publishNugetCmd = String.concat " " [
                "nuget"
                "push"
                nugetPackageFile
                "-s https://api.nuget.org/v3/index.json"
                $"-k {nugetApiKey}"
            ]

            if Shell.Exec("dotnet", publishNugetCmd) <> 0 then
                PublishResult.Failed(projectDir, "failed to publish the nuget package")
            else
                PublishResult.Ok(projectDir)

let publishSdks (projects: string list) =
    match env NUGET_PUBLISH_KEY with
    | None -> Error $"Missing environment variable {NUGET_PUBLISH_KEY} required to publish SDKs"
    | Some nugetApiKey ->
        match env PULUMI_VERSION with
        | None -> Error $"Missing environment variable {PULUMI_VERSION} required to publish SDKs"
        | Some pulumiVersion ->
            // found both a Pulumi version and a Nuget API key
            // use them to publish the SDKs
            Ok [ for projectDir in projects do publishSdk projectDir pulumiVersion nugetApiKey ]

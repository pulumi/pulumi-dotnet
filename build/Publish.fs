module Publish

open System
open System.IO
open System.Linq
open Fake.Core
open PulumiSdkVersion

let [<Literal>] NUGET_PUBLISH_KEY = "NUGET_PUBLISH_KEY"
let [<Literal>] PULUMI_VERSION = "PULUMI_VERSION"

let env (variableName: string) =
    let value = Environment.GetEnvironmentVariable variableName
    if String.isNullOrWhiteSpace value
    then None
    else Some value

let projectName (dir: string) = DirectoryInfo(dir).Name

[<RequireQualifiedAccess>]
type PublishResult =
    | Ok of project:string
    | Failed of project:string * error: string
    | AlreadyPublished of project:string

    member this.ProjectName() =
        match this with
        | Ok project -> projectName project
        | Failed (project, error) -> projectName project
        | AlreadyPublished project -> projectName project

    member this.HasErrored() =
        match this with
        | Failed _ -> true
        | otherwise -> false

let publishSdk (projectDir: string) (version: string) (nugetApiKey: string) (goSdkVersion: string) : PublishResult =
    let buildNugetCmd = String.concat " " [
        "build"
        "--configuration Release"
        $"-p:Version={version}"
        $"-p:PulumiSdkVersion={goSdkVersion}"
    ]

    if Shell.Exec("dotnet", buildNugetCmd, projectDir) <> 0 then
        PublishResult.Failed(projectDir, $"failed to build the project at {projectDir}")
    else
        let releaseDir = Path.Combine(projectDir, "bin", "Release")
        let releaseArtifacts = Directory.EnumerateFiles(releaseDir)
        let nugetPackageFile = releaseArtifacts.FirstOrDefault(
            (fun path -> path.Contains(version)), "")

        if nugetPackageFile = "" then
            PublishResult.Failed(projectDir, "couldn't find the nuget package")
        else
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

let publishSdks (projectDirs: string list) (pulumiSdkPath: string) =
    match env NUGET_PUBLISH_KEY with
    | None -> Error $"Missing environment variable {NUGET_PUBLISH_KEY} required to publish SDKs"
    | Some nugetApiKey ->
        match env PULUMI_VERSION with
        | None -> Error $"Missing environment variable {PULUMI_VERSION} required to publish SDKs"
        | Some pulumiVersion ->
            match findGoSDKVersion(pulumiSdkPath) with
            | None -> Error $"Could not find the Pulumi SDK version in {pulumiSdkPath} go.mod"
            | Some goSdkVersion ->
            // found a Pulumi version, sdk version and a Nuget API key
            // use them to publish the SDKs if they are not already on Nuget
            Ok [
                for projectDir in projectDirs do
                    let projectName = DirectoryInfo(projectDir).Name
                    if Nuget.exists projectName pulumiVersion then
                        PublishResult.AlreadyPublished(projectDir)
                    else
                        publishSdk projectDir pulumiVersion nugetApiKey goSdkVersion
            ]

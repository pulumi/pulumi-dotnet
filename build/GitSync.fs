module GitSync

open Fake.IO
open Fake.Core
open System.IO
open System

type SyncContent = {
    sourcePath: string list 
    destinationPath: string list
}

type SyncType =
    | File of SyncContent
    | Folder of SyncContent

let folder content = SyncType.Folder content
let file content = SyncType.File content

let formatSync = function
    | SyncType.Folder content 
    | SyncType.File content ->
        let source = String.concat "/" content.sourcePath
        let destination = String.concat "/" content.destinationPath
        $"Source: {source} -> Destination: {destination}"

type SyncOptions = {
    remoteRepository: string
    localRepositoryPath: string
    contents: SyncType list
}

let cloneRepository (remoteRepo: string) (workOnTempDir: string -> unit) =
    let tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()))
    try 
        if Shell.Exec("git", $"clone {remoteRepo} {tempDir.FullName}") = 0 then
            workOnTempDir(tempDir.FullName)
        else
            failwith $"Failed to clone {remoteRepo}"
    finally
        tempDir.Delete(true)

let repository (options: SyncOptions) =
    printfn $"Cloning {options.remoteRepository} to sync files and folders"
    cloneRepository options.remoteRepository (fun clonedRepo -> 
        for content in options.contents do
            match content with
            | SyncType.Folder folder -> 
                let sourcePath = Path.Combine [| clonedRepo; yield! folder.sourcePath |]
                let destination = Path.Combine [| options.localRepositoryPath; yield! folder.destinationPath |]
                Shell.copyDir destination sourcePath (fun file -> true)
                printfn $"Copied folder ({formatSync content})"
            | SyncType.File file ->
                let sourcePath = Path.Combine [| clonedRepo; yield! file.sourcePath |]
                let destination = Path.Combine [| options.localRepositoryPath; yield! file.destinationPath |]
                Shell.copyFile destination sourcePath
                printfn $"Copied file ({formatSync content})"
    )
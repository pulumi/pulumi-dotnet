module GitSync

open Fake.IO
open Fake.Core
open System.IO
open System

type FolderSync = {
    sourcePath: string list 
    destinationPath: string list
}

let formatFolder (folder: FolderSync) =
    let source = String.concat "/" folder.sourcePath
    let destination = String.concat "/" folder.sourcePath
    $"Source: {source} -> Destination: {destination}"

type SyncOptions = {
    remoteRepository: string
    folders: FolderSync list
    localRepositoryPath: string
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
    printfn $"Cloning {options.remoteRepository} to sync the following folders"
    for folder in options.folders do
        printfn $"{formatFolder folder}"

    cloneRepository options.remoteRepository (fun clonedRepo -> 
        for folder in options.folders do
            let sourcePath = Path.Combine [| clonedRepo; yield! folder.sourcePath |]
            let destination = Path.Combine [| options.localRepositoryPath; yield! folder.destinationPath |]
            Shell.copyDir destination sourcePath (fun file -> true)
    )
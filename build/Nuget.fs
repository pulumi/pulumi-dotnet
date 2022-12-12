module Nuget

open Newtonsoft
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System.Net.Http

let httpClient = new HttpClient()

let packageDataContainsVersion (packageData: JObject) (version: string) =
    if packageData.ContainsKey "versions" && packageData["versions"].Type = JTokenType.Array then
        let versions = packageData["versions"] :?> JArray
        versions
        |> Seq.cast<JObject>
        |> Seq.exists (fun ver -> ver.ContainsKey "version" && ver["version"].ToObject<string>() = version)
    else
        false

/// Checks whether a nuget package is already published with the specified version
let exists (package: string) (version: string) =
    let searchResults =
        httpClient.GetStringAsync($"https://azuresearch-usnc.nuget.org/query?q={package}")
        |> Async.AwaitTask
        |> Async.RunSynchronously
        |> JObject.Parse
        
    if not (searchResults.ContainsKey "data") || searchResults["data"].Type <> JTokenType.Array then
        false 
    else
    
    let data = searchResults["data"] :?> JArray
    
    data
    |> Seq.cast<JObject>
    |> Seq.exists (fun packageData ->
        if packageData.ContainsKey "id" && packageData["id"].ToObject<string>() = package
        then packageDataContainsVersion packageData version
        else false
    )
    
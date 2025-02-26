module PulumiSdkVersion

open System.IO
open System.Text.RegularExpressions

// Find the version of the Pulumi Go SDK that we are using for the language plugin.
let findGoSDKVersion (pulumiSdkPath: string) =
    let goMod = Path.Combine(pulumiSdkPath, "go.mod")
    try
        let lines = File.ReadAllLines(goMod)
        let patternRegex = new Regex("^\\s*github.com/pulumi/pulumi/sdk", RegexOptions.IgnoreCase)
        match Array.tryFind (fun (line: string) -> patternRegex.IsMatch(line)) lines with
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

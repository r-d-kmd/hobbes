open Argu
open FSharp.Data
open Hobbes.Web 
open Hobbes.Helpers



type CLIArguments =
    Collection of Workbench.Types.Collection
    | Host of string
    | PAT of string
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            Collection _ -> "indicates what collection of configuration should be published"
            | Host _ -> "The host to publish transformation and configurations to"
            | PAT _ -> "Master key or PAT to hobbes gateway"

let parse stmt =
    let stmt = stmt |> string
    Hobbes.Parsing.Parser.parse [stmt]
    |> Seq.exactlyOne

let getString pat url  = 
    Http.RequestString(url, 
                 httpMethod = "GET",
                 headers = 
                    [
                       HttpRequestHeaders.BasicAuth pat ""
                       HttpRequestHeaders.ContentType HttpContentTypes.Json
                    ]
    )
type ProcessResult = { exitCode : int; stdout : string; stderr : string }

let executeProcess (exe,cmdline) =
    let psi = System.Diagnostics.ProcessStartInfo(exe,cmdline) 
    psi.UseShellExecute <- false
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.CreateNoWindow <- true        
    let p = System.Diagnostics.Process.Start(psi) 
    let output = System.Text.StringBuilder()
    let error = System.Text.StringBuilder()
    p.OutputDataReceived.Add(fun args -> output.Append(args.Data) |> ignore)
    p.ErrorDataReceived.Add(fun args -> error.Append(args.Data) |> ignore)
    p.BeginErrorReadLine()
    p.BeginOutputReadLine()
    p.WaitForExit()
    { exitCode = p.ExitCode; stdout = output.ToString(); stderr = error.ToString() }

open Workbench.Types
[<EntryPoint>]
let main args =
   
    let arguments = 
        try
            let parser = ArgumentParser.Create<CLIArguments>(programName = "workbench")
            let arguments = parser.Parse args
            Some arguments
        with e -> 
            Log.excf e "Failed"
            None

    match arguments with
    None -> 0
    | Some arguments ->
        
        let collection = 
            match arguments.TryGetResult Collection with
            None -> 
                match (env "COLLECTION" "test").ToLower() with
                "production" -> Production
                | "development" -> Development
                | "all" -> All
                | "test" -> Test
                | e -> failwithf "Don't know collection %s" e
            | Some t -> t

        let host = 
            arguments.TryGetResult Host
            |> Option.orElse(
               match env "HOST" null with
               null -> failwith "personal access token must be provided"
               | host ->
                   host |> Some
            ) |> Option.get

        let pat = 
            arguments.TryGetResult PAT
            |> Option.orElse(
               match env "PAT" null with
               null -> failwith "personal access token must be provided"
               | masterKey ->
                   masterKey |> Some
            ) |> Option.get
            
            
        Log.logf "Using host: %s" host
        let urlTransformations = host + "/admin/transformation"
        let urlConfigurations = host + "/admin/configuration"

        Workbench.Configurations.State.initialise()
        Workbench.Configurations.DevOps.initialise()
        Workbench.Configurations.Test.initialise()

        let transformations = 
            allTransformations collection
            |> Seq.map Json.serialize

        let configurations = 
            printfn "Publishing %s-collection" (string collection)
            allConfigurations collection
            |> Seq.map (fun conf ->
                let trans = 
                   let ts = conf.Transformations |> List.map(fun t -> sprintf "%A" t.Name)
                   System.String.Join(",", ts)
                let source = 
                        match conf.Source with
                        Source.AzureDevOps p ->
                            sprintf """{
                                "name" : "azure devops",
                                "project" : "%s",
                                "account" : "%s"
                            }""" (string p) p.Account
                        | Source.Git(ds,p) ->
                            sprintf """{
                                "name" : "git",
                                "project" : "%s",
                                "account" : "%s",
                                "dataset" : "%s"
                            }""" (string p) p.Account (string ds)
                        | s -> failwithf "not supported yet. %A" s
                sprintf """{
                        "_id" : "%s",
                        "transformations" : [%s],
                        "source" : %s
                    }""" conf.Name trans source
            )
        let put url doc =
            Http.Request(url, 
                             httpMethod = "PUT",
                             body = TextRequest doc,
                             headers = 
                                [
                                   HttpRequestHeaders.BasicAuth pat ""
                                   HttpRequestHeaders.ContentType HttpContentTypes.Json
                                ]
                            ) |> ignore 
        transformations 
        |> Seq.iter(fun doc ->
            Log.logf "Creating transformation: %s" (Database.CouchDoc.Parse doc).Id
            
            try
                put urlTransformations doc
            with e ->
               Log.logf "Failed to publish transformations. URL: %s Msg: %s" urlTransformations e.Message
               reraise()
        )

        configurations
        |> Seq.iter(fun doc ->
            Log.logf "Creating configurations: %s" (Database.CouchDoc.Parse doc).Id
            put urlConfigurations doc
        )
        0
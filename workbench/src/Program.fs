

open Argu
open FSharp.Data
open Hobbes.Server.Db

type Environment = 
    Development
    | Production

type CLIArguments =
    Tests
    | Publish
    | Sync of string
    | Environment of Environment
    | PAT of string
    | AzureToken of string
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Tests -> "Flags that the tests should be run"
            | Publish -> "Publish the transformations to either development or production (set by environment)"
            | Sync _ -> "When sync-ing a project from azure"
            | Environment _ -> "Environment to publish transformations to"
            | PAT _ -> "The Private Access Token to use when posting transformations"
            | AzureToken _ -> "The token to use for authentication when syncronizing from Azure DevOps"

let parse stmt =
    let stmt = stmt |> string
    Hobbes.Parsing.Parser.parse [stmt]
    |> Seq.exactlyOne
    
[<EntryPoint>]
let main args =
   
    let results = 
        try
            let parser = ArgumentParser.Create<CLIArguments>(programName = "workbench")
            let results = parser.Parse args
            Some results
        with e -> 
            printfn "%s" e.Message
            None

    match results with
    None -> 0
    | Some results ->
        let environment = 
            match results.TryGetResult Environment with
            None -> Development
            | Some e -> e
        let environmentHost = 
            match environment with
            Development -> "http://localhost:8080"
            | Production -> "https://hobbes.azurewebsites.net"
            
        let test = results.TryGetResult Tests 
        let sync = results.TryGetResult Sync
        let publish = results.TryGetResult Publish

        if  test.IsSome || (sync.IsNone && publish.IsNone) then
            Workbench.Tests.test() |> ignore
            printfn "Press enter to exit..."
            System.Console.ReadLine().Length
        else            
            match sync with
            None -> 
                if publish |> Option.isSome then 
                    let urlTransformations = environmentHost + "/api/transformations"
                    let urlConfigurations = environmentHost + "/api/configurations"
                    let pat = results.GetResult PAT
                    let transformations = 
                        Workbench.Reflection.transformations()
                        |> Seq.map(fun (name,statements) ->
                            statements
                            |> List.map parse
                            |> ignore
                            
                            System.String.Join(",",
                                statements
                                |> List.map (fun stmt ->
                                   (stmt |> string).Replace("\"", "\\\"") |> sprintf "\n  %A"
                                )
                            ) |> sprintf "[%s\n]"
                            |> sprintf """{
                                "_id" : "%s",
                                "lines" : %s
                            }
                            """ name
                        )
                    let configurations = 
                        Workbench.Reflection.configurations()
                        |> Seq.map(fun c ->
                            sprintf """{
                                "_id" : "%s",
                                "source" : "%s",
                                "dataset" : "%s",
                                "transformations" : [%s]
                            }""" c.Id
                                 (c.Source |> Workbench.Source.string)
                                 (c.Project |> Workbench.Project.string)
                                 (System.String.Join(",",c.Transformations |> Seq.map(sprintf "%A")))
                        ) 
                    
                    transformations 
                    |> Seq.iter(fun doc ->
                        printfn "Creating transformation: %s" (Database.CouchDoc.Parse doc).Id
                        Http.Request(urlTransformations, 
                                     httpMethod = "PUT",
                                     body = TextRequest doc,
                                     headers = 
                                        [
                                           HttpRequestHeaders.BasicAuth pat ""
                                           HttpRequestHeaders.ContentType HttpContentTypes.Json
                                        ]
                                    ) |> ignore
                    )
                    configurations
                     
                    |> Seq.iter(fun doc ->
                        printfn "Creating configurations: %s" (Database.CouchDoc.Parse doc).Id
                        Http.Request(urlConfigurations, 
                                     httpMethod = "PUT",
                                     body = TextRequest doc,
                                     headers = 
                                        [
                                           HttpRequestHeaders.BasicAuth pat ""
                                           HttpRequestHeaders.ContentType HttpContentTypes.Json
                                        ]
                                    ) |> ignore
                    )
                0
             | Some configurationName ->
                let pat = results.GetResult PAT
                
                let url = environmentHost + "/api/sync/" + configurationName
                let azurePat = results.GetResult AzureToken
                Http.Request(url, 
                                 httpMethod = "GET",
                                 headers = 
                                    [
                                       HttpRequestHeaders.BasicAuth pat ""
                                       ("PAT",azurePat)
                                       HttpRequestHeaders.ContentType HttpContentTypes.Json
                                    ]
                                ) |> ignore
                0
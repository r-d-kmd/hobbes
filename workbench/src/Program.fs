

open Argu
open FSharp.Data


type ExecutionMode = 
    Tests
    | PublishTransformations

type Environment = 
    Development
    | Production

type CLIArguments =
    | ExecutionMode of ExecutionMode 
    | Environment of Environment
    | PAT of string
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | ExecutionMode _-> "Should the tests be run or should the transformations be published to the specified environment"
            | Environment _ -> "Environment to publish transformations to"
            | PAT _ -> "The Private Access Token to use when posting transformations"

let parse stmt =
    let stmt = stmt |> string
    Hobbes.Parsing.Parser.parse [stmt]
    |> Seq.exactlyOne

let transformations() = 
    let statements = 
        [
            "Gandalf.renaming",Gandalf.renaming
            "Azure.foldBySprint",Azure.foldBySprint
            "Metrics.stateCountBySprint",Metrics.stateCountBySprint
            "Metrics.expandingCompletionBySprint",Metrics.expandingCompletionBySprint
        ]
    statements
    |> List.map(fun (name,statements) ->
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
        let executionMode = 
            match results.TryGetResult ExecutionMode with
            None -> Tests
            | Some e -> e
        match executionMode with
        | Tests ->
            Workbench.Tests.test()
            |> printfn "%A"
            0
        | PublishTransformations ->
            let environment = 
                match results.TryGetResult Environment with
                None -> Development
                | Some e -> e
            let url = 
                match environment with
                  Development -> "http://localhost:8080"
                  | Production -> "https://hobbes.azurewebsites.net"
                + "/transformations"
            let pat = results.GetResult PAT
            transformations()
            |> List.iter(fun transformation ->
                Http.Request(url, 
                             httpMethod = "PUT",
                             body = TextRequest transformation,
                             headers = [HttpRequestHeaders.BasicAuth pat ""]
                            ) |> ignore
            )
            0
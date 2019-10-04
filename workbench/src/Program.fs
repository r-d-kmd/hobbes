
open Hobbes.DSL
open Hobbes.Parsing.AST
open Hobbes.FSharp

let parse stmt =
    let stmt = stmt |> string
    Hobbes.Parsing.Parser.parse [stmt]
    |> Seq.exactlyOne

[<EntryPoint>]
let main args =
    let statements = 
        [
            only (!> "WorkItemType" == !!> "User Story")
            pivot (!> "Iteration.IterationLevel3") (!> "StateCategory") Count (!> "WorkItemType")
        ]
    statements
    |> List.map parse
    |> ignore
    
    System.String.Join(",",
        statements
        |> List.map (fun stmt ->
           (stmt |> string).Replace("\"", "\\\"") |> sprintf "\n  %A"
        )
    ) |> sprintf "[%s\n]"
    |> printfn """{
        "_id" : "%s",
        "lines" : %s
    }
    """ args.[0]
    0
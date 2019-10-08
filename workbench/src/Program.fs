
open Hobbes.DSL
open Hobbes.Parsing.AST
open Hobbes.FSharp

let parse stmt =
    let stmt = stmt |> string
    Hobbes.Parsing.Parser.parse [stmt]
    |> Seq.exactlyOne

[<EntryPoint>]
let main args =
    if args |> Array.isEmpty then
        Tests.test()
        printfn "Press enter to exit..."
        System.Console.ReadLine() |> ignore
    else
        let statements = 
            match args.[0].ToLower() with
            "gandalf.renaming" ->
                Some Gandalf.renaming
            | "azure.foldbysprint" ->
                Some Azure.foldBySprint
            | _ ->
               printfn "Didn't find statements"
               None
        statements
        |> Option.iter(fun statements ->
            let name = args.[0]
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
            """ name
        )
    0
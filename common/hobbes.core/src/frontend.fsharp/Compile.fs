namespace Hobbes.FSharp

open Hobbes.Parsing
open Hobbes.FSharp.DataStructures
open FParsec.CharParsers

module Compile = 
    
    type Table = seq<string * seq<AST.KeyType * System.IComparable>>

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Table =
        let join table1 table2 = 
           table1
           |> Seq.append table2
           
    let parsedStatements statements = 
        statements
        |> Seq.fold(fun f' transform -> f' >> (fun (d : IDataMatrix) -> d.Transform transform)) id
        
    let statements (input : string): IDataMatrix -> IDataMatrix = 
        match input.Trim() with
        "" -> id
        | _ ->
            input
            |> StatementParser.parse
            |> parsedStatements
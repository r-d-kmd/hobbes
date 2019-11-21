namespace Hobbes.FSharp

open Hobbes.Parsing
open DataStructures

module Compile = 
    
    type Table = seq<string * seq<AST.KeyType * System.IComparable>>

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Table =
        let join table1 table2 = 
           table1
           |> Seq.append table2
           
    let parsedExpressions expressions = 
        expressions
        |> Seq.fold(fun f' transform -> f' >> (fun (d : IDataMatrix) -> d.Transform transform)) id
        
    let expressions (lines : #seq<string>) : IDataMatrix -> IDataMatrix = 
        let e = 
            match lines with
            l when l |> Seq.isEmpty  -> id
            | lines ->
                Parser.parse lines
                |> parsedExpressions
        fun table -> table |> e //(Why not just return e?)
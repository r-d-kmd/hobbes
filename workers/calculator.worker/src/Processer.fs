namespace Hobbes.Workers.Calculator

open Hobbes.Web
open Hobbes.Messaging.Broker
open Thoth.Json.Net

module Processer = 
    
    let private toMatrix (data : Cache.DataResult) = 
        let columnNames = data.ColumnNames
        data.Rows()
        |> Seq.mapi(fun index row ->
            index,row
                  |> Seq.zip columnNames
        ) |> Hobbes.FSharp.DataStructures.DataMatrix.fromRows
        
    let merge datasets =
        datasets
        |> Array.map toMatrix
        |> Array.reduce(fun res matrix ->
            res.Combine matrix
        )

    let join left right field =
        let left = 
            left
            |> toMatrix
        let right = 
            right
            |> toMatrix
        let data = 
            right |> left.Join field 
        data

    let transform statements originalData  =
        originalData
        |> toMatrix
        |> Hobbes.FSharp.Compile.expressions statements     

    let format format rows columnNames =
      
        let encodeValue value = 
            (match value with
             Cache.Value.Date dt -> 
                 dt
                 |> string
                 |> Encode.string
             | Cache.Value.Text t -> Encode.string t
             | Cache.Value.Int i -> Encode.int i
             | Cache.Value.Float f -> Encode.float f
             | Cache.Value.Boolean b -> Encode.bool b 
             | Cache.Value.Null -> Encode.nil)
        
        match format with
        | Json -> 
            rows 
            |> Array.map(fun row ->
                row
                |> Array.zip columnNames
                |> Array.map(fun (colName,(value : Cache.Value)) ->
                    colName,encodeValue value
                ) |> List.ofArray
                |> Encode.object
            )
#nowarn "3061"
namespace LocalData

open FSharp.Data
open Hobbes.Web
open Hobbes.Web.RawdataTypes
open Hobbes.Web.Routing

[<RouteArea ("/", false)>]
module Data =
    type internal LocalDataProviderConfig = JsonProvider<"""{
                "provider" : "local",
                "id" : "lkjlkj", 
                "columns" : [
                    "prop1",
                    "prop2",
                    "setup"], 
                "rows" : [
                    [2,"lkjlkj",false],
                    ["lkjlkj",2.003,{"d":0}]
                ]
            }""">

    let synchronize (source : LocalDataProviderConfig.Root) =
        let body = 
            let columnNames = 
                source.Columns
            let rows = 
                source.Rows
                |> Array.map(fun row ->
                    match row.JsonValue with
                    JsonValue.Array cells ->
                        cells
                        |> Array.map(fun cell ->
                            match cell with
                            JsonValue.String s -> s :> obj
                            | JsonValue.Number n -> n :> obj
                            | JsonValue.Float f -> f :> obj
                            | JsonValue.Boolean b -> b :> obj
                            | JsonValue.Null  -> null :> obj
                            | _ -> failwith "Must be a simple value"
                        )
                    | v -> failwithf "Expected an array but got (%s)" (v.ToString())
                )
            {
               ColumnNames = columnNames
               Values = rows
               RowCount = rows.Length
            } : Cache.DataResult
    
        (source.Id, body)
        
    

    [<Post("config", true)>]
    let loadConfig conf =
        let key,data = 
            conf
            |> LocalDataProviderConfig.Parse
            |> synchronize
        let data = Cache.createCacheRecord key [] data
        match Http.post (Http.UniformData Http.Update) (data.ToString()) with
        Http.Success _ -> 200,"ok"
        | Http.Error(sc, msg) -> sc,msg
        
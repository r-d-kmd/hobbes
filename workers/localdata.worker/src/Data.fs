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
                "data" : [{
                    "prop1":2,
                    "prop2":"lkjlkj",
                    "setup" : false
                },{
                    "prop1":2,
                    "prop2":"lkjlkj",
                    "setup" : false
                }]
            }""">

    let synchronize (source : LocalDataProviderConfig.Root) =
        let body = 
            let columnNames, rows = 
                source.Data
                |> Array.fold(fun (columnNames, rows) o ->
                    let props = 
                        match o.JsonValue with
                        JsonValue.Record props ->
                            props
                        | v -> failwithf "Expected a record but got (%s)" (v.ToString())
                    let cns = 
                        props
                        |> Array.fold(fun cn (k, _) -> cn |> Set.add k) columnNames
                    cns,(props
                        |> Map.ofArray)::rows
                )(Set.empty,[])
            {
               ColumnNames = columnNames |> Set.toArray
               Values = 
                   rows
                   |> List.map(fun row ->
                       columnNames
                       |> Set.toArray
                       |> Array.map(fun columnName ->
                           match row |> Map.tryFind columnName with
                           None -> null
                           | Some v -> 
                               match v with
                               JsonValue.String s -> s :> obj
                               | JsonValue.Number n -> n :> obj
                               | JsonValue.Float f -> f :> obj
                               | JsonValue.Boolean b -> b :> obj
                               | JsonValue.Null  -> null :> obj
                               | _ -> failwith "Must be a simple value"
                       
                       )
                   ) |> List.toArray
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
        
open Hobbes.Helpers.Environment
open Readers.AzureDevOps.Data
open Readers.AzureDevOps
open Hobbes.Web
open Hobbes.Messaging
open Hobbes.Messaging.Broker
open FSharp.Data
open Thoth.Json.Net

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
    |> Some
    

let handleMessage message =
    match message with
    Empty -> Success
    | Sync sourceDoc -> 
        Log.debugf "Received message. %s" sourceDoc
        try
            let source = sourceDoc |> LocalDataProviderConfig.Parse
            
            match synchronize source with
            None -> 
                sprintf "Conldn't syncronize. %s" sourceDoc
                |> Failure 
            | Some (key,data) -> 
                let data = Cache.createCacheRecord key [] data
                match Http.post (Http.UniformData Http.Update) (data.ToString()) with
                Http.Success _ -> 
                   Log.logf "Data uploaded to cache"
                   Success
                | Http.Error(status,msg) -> 
                    sprintf "Upload of %s to uniform data failed. %d %s" (data.ToString()) status msg
                    |> Failure
        with e ->
            Log.excf e "Failed to process message"
            Excep e
            
[<EntryPoint>]
let main _ =
    Database.awaitDbServer()
    async{    
        do! awaitQueue()
        Broker.LocalData handleMessage
    } |> Async.RunSynchronously
    0
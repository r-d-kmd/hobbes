open Hobbes.Web
open Hobbes.Messaging.Broker
open Hobbes.Messaging
open FSharp.Json

let cache = Cache.Cache(Http.UniformData)
let transformData (message : CalculationMessage) =
    match message with
    Transform message -> 
        let cacheKey = message.CacheKey
        let transformation = message.Transformation
        match cache.Get message.CacheKey with
            None -> 
                Log.logf "No data for that key (%s)" cacheKey
                false
            | Some cacheRecord -> 
                if cacheRecord.CacheKey <> cacheKey then
                    failwithf "Wrong data returned. Got %s expected %s" cacheRecord.CacheKey cacheKey
                let transformedData = 
                    try
                        let columnNames = cacheRecord.Data.ColumnNames
                        let data = 
                            cacheRecord.Data.Rows
                            |> Seq.mapi(fun index row ->
                                index,row
                                      |> Seq.zip columnNames
                            )
                         
                        data
                        |> Hobbes.FSharp.DataStructures.DataMatrix.fromRows
                        |> Hobbes.FSharp.Compile.expressions transformation.Statements 
                        |> Hobbes.FSharp.DataStructures.DataMatrix.toJson Hobbes.FSharp.DataStructures.Rows 
                        |> Some
                    with e ->
                        Log.excf e "Failed to transform data using [%s] on [%s]" transformation.Name cacheKey
                        None    

                let key = cacheKey + ":" + transformation.Name
                try
                    transformedData
                    |> Option.bind(Json.deserialize<Cache.DataResult> >> cache.InsertOrUpdate key >> Some)
                    |> Option.bind(fun _ -> 
                        Log.logf "Transformation of [%s] using [%s] resulting in [%s] completed" cacheKey transformation.Name key
                        Some(true)
                    ) |> Option.isSome
                with e ->
                   Log.excf e "Couldn't insert data (key: %s)." key
                   false

[<EntryPoint>]
let main _ =
    async{    
        do! awaitQueue()
        Broker.Calculation transformData
    } |> Async.RunSynchronously
    0
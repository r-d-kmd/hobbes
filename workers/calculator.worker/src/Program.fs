open Hobbes.Web
open Hobbes.Messaging.Broker
open Hobbes.Messaging
open Hobbes.Helpers

let cache = Cache.Cache(Http.UniformData)
let transformData (message : CalculationMessage) =
    match message with
    Transform message -> 
        let cacheKey = message.CacheKey
        let transformation = message.Transformation
        match cache.Get message.CacheKey with
        None -> 
            Log.logf "No data for that key (%s)" cacheKey
            Success
        | Some cacheRecord -> 
            try
                let columnNames = cacheRecord.Data.ColumnNames
                let data = 
                    cacheRecord.Data.Rows()
                    |> Seq.mapi(fun index row ->
                        index,row
                              |> Seq.zip columnNames
                    )
                let key = cacheKey + ":" + transformation.Name
                let dataJson = 
                    data
                    |> Hobbes.FSharp.DataStructures.DataMatrix.fromRows
                    |> Hobbes.FSharp.Compile.expressions transformation.Statements 
                    |> Hobbes.FSharp.DataStructures.DataMatrix.toJson Hobbes.FSharp.DataStructures.Rows                 
                let transformedData = 
                    try
                        dataJson
                        |> Json.deserialize<Cache.DataResult> 
                    with e ->
                        Log.excf e "Couldn't deserialize (%s)" dataJson
                        reraise()
                try
                    transformedData
                    |> cache.InsertOrUpdate key
                    Log.logf "Transformation of [%s] using [%s] resulting in [%s] completed" cacheKey transformation.Name key
                    Success
                with e ->
                   sprintf "Couldn't insert data (key: %s)." key
                   |> Failure 
            with e ->
                Log.excf e "Failed to transform data using [%s] on [%s]" transformation.Name cacheKey
                Excep e    

[<EntryPoint>]
let main _ =
    async{    
        do! awaitQueue()
        Broker.Calculation transformData
    } |> Async.RunSynchronously
    0
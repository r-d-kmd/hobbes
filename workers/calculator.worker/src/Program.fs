open Hobbes.Web
open Hobbes.Messaging.Broker
open Hobbes.Messaging
open Hobbes.Helpers

let cache = Cache.Cache(Http.UniformData) :> Cache.ICacheProvider
let fromCache cacheKey =
    let cacheRecord = 
        cacheKey
        |> cache.Get
        |> Option.get

    let columnNames = cacheRecord.Data.ColumnNames
    cacheRecord.Data.Rows()
    |> Seq.mapi(fun index row ->
        index,row
              |> Seq.zip columnNames
    ) |> Hobbes.FSharp.DataStructures.DataMatrix.fromRows

let transformData (message : CalculationMessage) =
    try
        let key,dependsOn,data = 
            match message with
            Merge message ->
                let cacheKey = message.CacheKey
                let dependsOn = message.Datasets |> Array.toList
                let data = 
                    message.Datasets
                    |> Array.map fromCache
                    |> Array.reduce(fun res matrix ->
                        res.Combine matrix
                    )
                cacheKey,dependsOn,data
            | Join message ->
                let cacheKey = message.CacheKey
                let dependsOn = [message.Left;message.Right]
                let left = 
                    message.Left
                    |> fromCache
                let right = 
                    message.Right
                    |> fromCache
                let data = 
                    right |> left.Join message.Field 
                cacheKey,dependsOn,data
            | Transform message -> 
                let dependsOn = message.DependsOn
                let transformation = message.Transformation
                let key = dependsOn + ":" + transformation.Name
                key,[dependsOn], 
                   dependsOn
                   |> fromCache
                   |> Hobbes.FSharp.Compile.expressions transformation.Statements     
                   
        let dataJson = 
            data
            |> Hobbes.FSharp.DataStructures.DataMatrix.toJson Hobbes.FSharp.DataStructures.Rows                 
        let transformedData = 
            try
                dataJson
                |> Json.deserialize<Cache.DataResult> 
            with e ->
                Log.excf e "Couldn't deserialize (%s)" dataJson
                reraise()
        transformedData
        |> cache.InsertOrUpdate key dependsOn
        Log.logf "Transformation of [%A] using [%A] resulting in [%s] completed" dependsOn message key
        Success
    with e ->
       Log.errorf "Couldn't insert data (%A)." message
       Excep e 

[<EntryPoint>]
let main _ =
    async{    
        do! awaitQueue()
        Broker.Calculation transformData
    } |> Async.RunSynchronously
    0
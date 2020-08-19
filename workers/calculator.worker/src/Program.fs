open Hobbes.Web
open Hobbes.Messaging.Broker
open Hobbes.Messaging
open Hobbes.Helpers
open Hobbes.Workers.Calculator.Processer

let fromCache cacheKey =
    match Http.get (cacheKey |> Http.UniformDataService.Read |> Http.UniformData) Json.deserialize<Cache.CacheRecord> with
    Http.Error(sc,msg) -> 
        if sc = 404 then 
            failwithf "No data for that key (%s)" cacheKey
        else
            failwithf "Failed retrieving data %d - %s" sc msg
    | Http.Success cacheRecord -> 
        cacheRecord.Data

let insertOrUpdate doc = 
    match Http.post (Http.UniformDataService.Update |> Http.UniformData) doc with
        Http.Error(sc,msg) -> 
            failwithf "Failed inserting/updating data %d - %s" sc msg
        | Http.Success _ -> 
            Log.log "Data inserted"
            
let transformData (message : CalculationMessage) =
    let insertMatrix key dependsOn data = 
        try
            let dataJson = 
                data
                |> Hobbes.FSharp.DataStructures.DataMatrix.toJson Hobbes.FSharp.DataStructures.Rows                 
            let transformedData = 
                dataJson
                |> Json.deserialize<Cache.DataResult> 

            assert(transformedData.ColumnNames.Length =  transformedData.Values.[0].Length)
            assert(transformedData.Values.Length =  transformedData.RowCount)

            transformedData
            |> Cache.createCacheRecord key dependsOn
            |> Json.serialize
            |> insertOrUpdate 
            Log.logf "Transformation of [%A] using [%A] resulting in [%s] completed" dependsOn message key
            Success
        with e ->
         Excep e
    try
        match message with
        Merge message ->
            let cacheKey = message.CacheKey
            let dependsOn = message.Datasets |> Array.toList
            message.Datasets
            |> Array.map fromCache 
            |> merge
            |> insertMatrix cacheKey dependsOn
        | Join message ->
            let cacheKey = message.CacheKey
            let dependsOn = [message.Left;message.Right]
            let left = 
                message.Left
                |> fromCache
            let right = 
                message.Right
                |> fromCache
            
            join left right message.Field 
            |> insertMatrix cacheKey dependsOn
        | Transform message -> 
            let dependsOn = message.DependsOn
            let transformation = message.Transformation
            let key = dependsOn + ":" + transformation.Name
            dependsOn
            |> fromCache
            |> transform message.Transformation.Statements
            |> insertMatrix key [dependsOn] 
        | Format message ->
            let cacheKey = message.CacheKey
            let record = 
                try
                    message.CacheKey
                    |> fromCache
                    |> Some
                with e ->
                    Log.excf e "Couldn't load data for formatting"
                    None
            let key = sprintf "%s:%A" cacheKey format
            match record with
            Some record ->
                let formatted = format message.Format record.Values record.ColumnNames
                let data = 
                    formatted
                    |> Cache.createDynamicCacheRecord key [cacheKey]
                match 
                    data
                    |> Http.post (Http.UpdateFormatted |> Http.UniformData) with
                Http.Success _ -> 
                    Log.logf "Formatting of [%s] to [%A] resulting in [%s] completed" cacheKey format key
                    Success
                | Http.Error(sc,msg) ->
                    sprintf "Trying posting data (%s) to uniform put failed with %d - %s"  (data.JsonValue.ToString().Substring(min (data.JsonValue.ToString().Length) 500)) sc msg
                    |> Failure
            | None -> 
                sprintf "Couldn't get cached data. See log for details. cache key: %s" cacheKey
                |> Failure
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
open Hobbes.Web
open Hobbes.Messaging.Broker
open Hobbes.Messaging
open Hobbes.Helpers
open Hobbes.Workers.Calculator.Processer

let fromCache cacheKey =
    match Http.get (cacheKey |> Http.UniformDataService.Read |> Http.UniformData) Cache.CacheRecord.OfJson with
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
    let insertMatrix key dependsOn transformationName data = 
        
        let dataJson = 
            data
            |> Hobbes.FSharp.DataStructures.DataMatrix.toJson
            |> Thoth.Json.Net.Encode.toString 0                
        let transformedData = 
            try
                dataJson
                |> Cache.DataResult.OfJson 
                |> Some
            with e ->
               Log.excf e "Couldn't deserialize data %s" dataJson
               None
        transformedData
        |> Option.bind(fun transformedData ->
            assert(transformedData.ColumnNames.Length =  transformedData.Values.[0].Length)
            assert(transformedData.Values.Length =  transformedData.RowCount)
            try
                (transformedData
                 |> Cache.createCacheRecord key dependsOn).ToString()
                
                |> insertOrUpdate 
                Log.logf "Transformation of [%A] using [%s] resulting in [%s] completed" dependsOn transformationName key
                Success |> Some
            with e ->
                Log.excf e "Couldn't insert data (%A)." message
                Excep e |> Some
        ) |> Option.orElse(dataJson |> sprintf "Couldn't deserialize data %s" |> Failure |> Some)
        |> Option.get 
    
    match message with
    Merge message ->
        try
            let cacheKey = message.CacheKey
            let dependsOn = message.Datasets |> Array.toList
            message.Datasets
            |> Array.map fromCache 
            |> merge
            |> insertMatrix cacheKey dependsOn (System.String.Join("+",message.Datasets))
        with e ->
            Log.excf e "Merge failed"
            Excep e
    | Join message ->
        try
            let cacheKey = message.CacheKey
            let dependsOn = [message.Left;message.Right]
            let left = 
                message.Left
                |> fromCache
            let right = 
                message.Right
                |> fromCache
            
            join left right message.Field 
            |> insertMatrix cacheKey dependsOn (message.Left + "->" + message.Right )
        with e ->
           Log.exc e "Join failed"
           Excep e
    | Transform message -> 
        try
            let dependsOn = message.DependsOn
            let transformation = message.Transformation
            let key = dependsOn + ":" + transformation.Name
            
            dependsOn
            |> fromCache
            |> transform (message.Transformation.Statements
                          |> Seq.map(fun s -> s.Replace("\\","\\\\"))
                         )
            |> insertMatrix key [dependsOn] message.Transformation.Name
        with e ->
            Log.exc e "Transformation failed"
            Excep e
    | Format message ->
        try
            let cacheKey = message.CacheKey
            let record = 
                try
                    message.CacheKey
                    |> fromCache
                    |> Some
                with e ->
                    Log.excf e "Couldn't load data for formatting"
                    None
            let key = sprintf "%s:%A" cacheKey message.Format
            match record with
            Some record ->
                let formatted = format message.Format record.Values record.ColumnNames
                let data = 
                    formatted
                    |> Cache.createDynamicCacheRecord key [cacheKey]
                match 
                    data.JsonValue.ToString()
                    |> Http.post (Http.UpdateFormatted |> Http.UniformData) with
                Http.Success _ -> 
                    Log.logf "Formatting of [%s] to [%A] resulting in [%s] completed" cacheKey message.Format key
                    Success
                | Http.Error(sc,msg) ->
                    sprintf "Trying posting data (%s) to uniform put failed with %d - %s"  (data.JsonValue.ToString().Substring(0,min (data.JsonValue.ToString().Length) 500)) sc msg
                    |> Failure
            | None -> 
                sprintf "Couldn't get cached data. See log for details. cache key: %s" cacheKey
                |> Failure
        with e ->
            Log.exc e "Couldn't create json dataset"
            Excep e


[<EntryPoint>]
let main _ =
    async{    
        do! awaitQueue()
        Broker.Calculation transformData
    } |> Async.RunSynchronously
    0
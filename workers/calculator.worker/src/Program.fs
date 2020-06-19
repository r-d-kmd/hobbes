open Hobbes.Web
open Hobbes.Messaging.Broker
open Hobbes.Messaging
open Hobbes.Helpers

let rowToString =
    function
      Cache.Value.Int i -> sprintf "%i" i
    | Cache.Value.Float f -> sprintf "%f" f
    | Cache.Value.Date d -> sprintf "\"%A\"" d
    | Cache.Value.Text s -> s |> Json.serialize
                            |> sprintf "%s"
    | Cache.Value.Null -> "null"
    | Cache.Value.Boolean b -> sprintf "%b" b

let formatToJson rows names =
        rows
        |> Array.map (fun vals -> 
            vals |> Array.map2 (fun n v -> sprintf "\"%s\" : %s" n (rowToString v)) names
            |> String.concat ","
        )
        |> String.concat "},{"
        |> sprintf "[{%s}]"

let cache = Cache.DataResultCache(Http.UniformData)
let dataSet = Cache.GenericCache(Http.DataSet)
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
    | Format message ->
        let cacheKey = message.CacheKey
        let format = message.Format
        match cache.Get cacheKey with
        None -> 
            Log.logf "No data for that key (%s)" cacheKey
            Success
        | Some record -> 
                let names = record.Data.ColumnNames
                let rows = record.Data.Values
                let key = sprintf "%s:%A" cacheKey format
                let formatted =
                    match format with
                    | Json -> formatToJson rows names
                try
                    formatted
                    |> dataSet.InsertOrUpdate key
                    Log.logf "Formatting of [%s] to [%A] resulting in [%s] completed" cacheKey format key
                    Success
                with e ->
                   sprintf "Couldn't insert data (key: %s)." key
                   |> Failure 


[<EntryPoint>]
let main _ =
    async{    
        do! awaitQueue()
        Broker.Calculation transformData
    } |> Async.RunSynchronously
    0
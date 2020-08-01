open Hobbes.Web
open Hobbes.Messaging.Broker
open Hobbes.Messaging
open Hobbes.Helpers


let fromCache cacheKey =
    match Http.get (cacheKey |> Http.UniformDataService.Read |> Http.UniformData) Json.deserialize<Cache.CacheRecord> with
    Http.Error(sc,msg) -> 
        if sc = 404 then 
            failwithf "No data for that key (%s)" cacheKey
        else
            failwithf "Failed retrieving data %d - %s" sc msg
    | Http.Success cacheRecord -> 
        cacheRecord

let toMatrix (cacheRecord : Cache.CacheRecord) = 
    let columnNames = cacheRecord.Data.ColumnNames
    cacheRecord.Data.Rows()
    |> Seq.mapi(fun index row ->
        index,row
              |> Seq.zip columnNames
    ) |> Hobbes.FSharp.DataStructures.DataMatrix.fromRows

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
                try
                    dataJson
                    |> Json.deserialize<Cache.DataResult> 
                with e ->
                    Log.excf e "Couldn't deserialize (%s)" dataJson
                    reraise()
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
                let data = 
                    message.Datasets
                    |> Array.map (fromCache >> toMatrix)
                    |> Array.reduce(fun res matrix ->
                        res.Combine matrix
                    )
                insertMatrix cacheKey dependsOn data
            | Join message ->
                let cacheKey = message.CacheKey
                let dependsOn = [message.Left;message.Right]
                let left = 
                    message.Left
                    |> (fromCache >> toMatrix)
                let right = 
                    message.Right
                    |> (fromCache >> toMatrix)
                let data = 
                    right |> left.Join message.Field 
                insertMatrix cacheKey dependsOn data
            | Transform message -> 
                let dependsOn = message.DependsOn
                let transformation = message.Transformation
                let key = dependsOn + ":" + transformation.Name
                dependsOn
                |> (fromCache >> toMatrix)
                |> Hobbes.FSharp.Compile.expressions transformation.Statements     
                |> insertMatrix key [dependsOn] 
            | Format message ->
                let cacheKey = message.CacheKey
                let format = message.Format
                let record = 
                    try
                        message.CacheKey
                        |> fromCache 
                        |> Some
                    with e ->
                        Log.excf e "Couldn't load data for formatting"
                        None
                let formatToJson rows names = 
                        rows
                        |> Array.map(fun row ->
                            System.String.Join(",",
                                row
                                |> Array.zip names
                                |> Array.map(fun (colName,(value : Cache.Value)) ->
                                    let valueAsString = 
                                        match value with
                                        Cache.Value.Date dt -> 
                                            dt
                                            |> string
                                            |> sprintf """ "%s" """
                                        | Cache.Value.Text t -> sprintf """ "%s" """ t
                                        | Cache.Value.Int i -> sprintf "%d" i 
                                        | Cache.Value.Float f -> sprintf "%f" f
                                        | Cache.Value.Boolean b -> sprintf "%b" b 
                                        | Cache.Value.Null -> "null"
                                    sprintf """ "%s" : %s """ colName valueAsString
                                ))
                            |> sprintf "{%s}"
                            |> FSharp.Data.JsonValue.Parse
                        )
                record
                |> Option.bind(fun record ->
                    let names = record.Data.ColumnNames
                    let rows = record.Data.Values
                    let key = sprintf "%s:%A" cacheKey format
                    let formatted = 
                        match format with
                        | Json -> 
                            formatToJson rows names
                    try
                        (formatted
                         |> Cache.createDynamicCacheRecord key record.DependsOn).JsonValue
                        |> string
                        |> Http.put (Http.Update |> Http.UniformData)
                        |> ignore
                        Log.logf "Formatting of [%s] to [%A] resulting in [%s] completed" cacheKey format key
                        Success
                        |> Some
                    with e ->
                       sprintf "Couldn't insert data (key: %s)." key
                       |> Failure
                       |> Some
                ) |> Option.orElse(Some Success)
                |> Option.get
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
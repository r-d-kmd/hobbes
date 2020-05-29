open System
open System.Text
open Hobbes.Helpers.Environment
open Hobbes.Web
open Hobbes.Web.RawdataTypes
open Hobbes.Web.Cache
open Hobbes.Messaging.Queue

type DependingTransformationList = FSharp.Data.JsonProvider<"""[
    {
        "_id" : "lkjlkj",
        "lines" : ["lkjlkj", "lkjlkj","o9uulkj"]
    }
]""">
type CalculationMessage = FSharp.Data.JsonProvider<"""{
        "transformation" : {
            "_id" : "lkjlkj",
            "lines" : ["lkjlkj", "lkjlkj","o9uulkj"]
        },
        "cacheKey" : "lkjlkjl"
    }""">
let cache = Cache.Cache(Http.UniformData)
let transformData message =
    let message = CalculationMessage.Parse message 
    let cacheKey = message.CacheKey
    let transformation = message.Transformation
    match cache.Get message.CacheKey with
        None -> 
            Log.logf "No data for that key (%s)" cacheKey
            false
        | Some cacheRecord -> 
            if cacheRecord.Id <> cacheKey then
                failwithf "Wrong data returned. Got %s expected %s" cacheRecord.Id cacheKey
            let transformedData = 
                try
                    let data = 
                        cacheRecord
                        |> Cache.readData
                     
                    data
                    |> Hobbes.FSharp.DataStructures.DataMatrix.fromRows
                    |> Hobbes.FSharp.Compile.expressions transformation.Lines 
                    |> Hobbes.FSharp.DataStructures.DataMatrix.toJson Hobbes.FSharp.DataStructures.Rows 
                    |> Some
                with e ->
                    Log.excf e "Failed to transform data using [%s] on [%s]" transformation.Id cacheKey
                    None    

            let key = cacheKey + ":" + transformation.Id
            try
                transformedData
                |> Option.bind(Cache.DataResult.Parse >> cache.InsertOrUpdate key >> Some)
                |> Option.bind(fun _ -> 
                    Log.logf "Transformation of [%s] using [%s] resulting in [%s] completed" cacheKey transformation.Id key
                    Some(true)
                ) |> Option.isSome
            with e ->
               Log.excf e "Couldn't insert data (key: %s)." key
               false
let getDependingTransformations cacheKey = 
    try
        match cache.Get cacheKey with
        None -> 
            Log.logf "No data for that key (%s)" cacheKey
            false
        | Some cacheRecord -> 
            if cacheRecord.Id <> cacheKey then
                failwithf "Wrong data returned. Got %s expected %s" cacheRecord.Id cacheKey
            let service = cacheKey |> Http.DependingTransformations |> Http.Configurations
            match Http.get service DependingTransformationList.Parse  with
            Http.Success transformations ->
                transformations
                |> Seq.iter(fun transformation ->
                    let msg = sprintf """{"transformation": "%s", "cacheKey" : "%s"}""" (transformation.JsonValue.ToString()) cacheKey
                    try
                        publish Queue.Calculation msg
                    with e ->
                        //todo put on dead letter queue
                        Log.excf e "Failed to publish message to calculation queue. %s" msg
                )
                true
            | Http.Error(404,_) ->
                Log.debug "No depending transformations found."
                true
            | Http.Error(sc,m) ->
                Log.errorf  "Failed to transform data (%s) %d %s" cacheKey sc m
                false
    with e ->
        Log.excf e "Failed to perform calculation."
        false
[<EntryPoint>]
let main _ =
    
    async{    
        do! awaitQueue()
    } |> Async.RunSynchronously
    async{
        watch Queue.Cache getDependingTransformations 5000
    } |> Async.Start
    watch Queue.Calculation transformData 5000
    0
open System
open System.Text
open Hobbes.Helpers.Environment
open Hobbes.Web
open Hobbes.Web.RawdataTypes
open Hobbes.Web.Cache
open Hobbes.Workers.Shared.Queue

type DependingTransformationList = FSharp.Data.JsonProvider<"""[
    {
        "_id" : "lkjlkj",
        "lines" : ["lkjlkj", "lkjlkj","o9uulkj"]
    }
]""">

let cache = Cache.Cache(Http.UniformData)
let handleMessage cachedData = 
    try
        let cacheRecord = Hobbes.Web.Cache.DataResult.Parse cachedData
        let cacheKey = cacheRecord.Id
        let service = cacheKey |> Http.DependingTransformations |> Http.Configurations
        match Http.get service DependingTransformationList.Parse  with
        Http.Success transformations ->
            transformations
            |> Seq.fold(fun r transformation ->
                let key = cacheKey + ":" + transformation.Id
                try
                    cachedData
                    |> Cache.DataResult.Parse
                    |> Cache.readData
                    |> Hobbes.FSharp.DataStructures.DataMatrix.fromRows
                    |> Hobbes.FSharp.Compile.expressions transformation.Lines 
                    |> Hobbes.FSharp.DataStructures.DataMatrix.toJson Hobbes.FSharp.DataStructures.Rows 
                    |> createCacheRecord key
                    |> cache.InsertOrUpdate
                    printfn "Transformation (%s) completed" key
                    r && true 
                with e ->
                   eprintfn "Couldn't insert data (key: %s). %s %s" key e.Message e.StackTrace
                   false
            ) true
        | Http.Error(404,m) ->
            printfn "No depending transformations found. Message: %s" m
            true
        | Http.Error(sc,m)
            printfn "Failed to transform data (%s) %d %s" cacheKey sc m
            false
    with e ->
        printfn "Failed to perform calculation. %s %s" e.Message e.StackTrace
        reraise()
[<EntryPoint>]
let main _ =
    watch Queue.Cache handleMessage 5000
    0
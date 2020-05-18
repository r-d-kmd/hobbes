open System
open System.Text
open Hobbes.Helpers.Environment
open Hobbes.Web
open Hobbes.Web.RawdataTypes
open Hobbes.Web.Cache
open Hobbes.Workers.Shared.Queue

type DependingTransformationList = FSharp.Data.JsonProvider<"""[
    {
        "name" : "lkjlkj",
        "lines" : ["lkjlkj", "lkjlkj","o9uulkj"]
    }
]""">

let cache = Cache.Cache(Http.UniformData)
let handleMessage message = 
    let record = 
        message
        |> CacheRecord.Parse
    let cachedData = 
        record
        |> Cache.readData
        |> Hobbes.FSharp.DataStructures.DataMatrix.fromRows 
    let cacheKey = record.Id 
    let service = cacheKey |> Http.DependingTransformations |> Http.Configurations
    match Http.get service DependingTransformationList.Parse  with
    Http.Success transformations ->
        transformations
        |> Seq.fold(fun r transformation ->
            let key = cacheKey + ":" + transformation.Name
            try
                cachedData
                |> Hobbes.FSharp.Compile.expressions transformation.Lines 
                |> Hobbes.FSharp.DataStructures.DataMatrix.toJson Hobbes.FSharp.DataStructures.Rows 
                |> createCacheRecord key
                |> cache.InsertOrUpdate
                r && true
            with e ->
               eprintfn "Couldn't insert data (key: %s). %s %s" key e.Message e.StackTrace
               false
        ) true
    | Http.Error(sc,m) ->
        eprintfn "Calculation failed. Couldn't get transformation. %d - %s" sc m
        false
[<EntryPoint>]
let main _ =
    watch Queue.Cache handleMessage
    printfn "Waiting for calculation messages"
    printfn "Press enter to exit"
    let a = Console.ReadLine() 
    printfn "%s" a
    0
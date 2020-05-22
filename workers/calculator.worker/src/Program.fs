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
let handleMessage cacheKey = 
    try
        match cache.Get cacheKey with
        None -> 
            Log.logf "No data for that key (%s)" cacheKey
            false
        | Some cacheRecord -> 
            let service = cacheKey |> Http.DependingTransformations |> Http.Configurations
            match Http.get service DependingTransformationList.Parse  with
            Http.Success transformations ->
                transformations
                |> Seq.fold(fun r transformation ->
                    let key = cacheKey + ":" + transformation.Id
                    try
                        let data = 
                            cacheRecord
                            |> Cache.readData

                        data
                        |> Hobbes.FSharp.DataStructures.DataMatrix.fromRows
                        |> Hobbes.FSharp.Compile.expressions transformation.Lines 
                        |> Hobbes.FSharp.DataStructures.DataMatrix.toJson Hobbes.FSharp.DataStructures.Rows 
                        |> Cache.DataResult.Parse
                        |> cache.InsertOrUpdate key
                        Log.logf "Transformation (%s) completed" key
                        r && true 
                    with e ->
                       Log.excf e "Couldn't insert data (key: %s)." key
                       false
                ) true
            | Http.Error(404,_) ->
                Log.debug "No depending transformations found."
                true
            | Http.Error(sc,m) ->
                Log.errorf null "Failed to transform data (%s) %d %s" cacheKey sc m
                false
    with e ->
        Log.excf e "Failed to perform calculation."
        false
[<EntryPoint>]
let main _ =
    watch Queue.Cache handleMessage 5000
    0
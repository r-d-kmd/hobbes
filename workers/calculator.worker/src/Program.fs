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

let cache = Cache.Cache(Http.UniformData)
let handleMessage cacheKey = 
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
                |> Seq.fold(fun r transformation ->
                    r
                    |> Option.bind(fun r -> 
                        
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
                                Some(r && true)
                            )
                        with e ->
                           Log.excf e "Couldn't insert data (key: %s)." key
                           Some false
                    )
                ) (Some true)
                |> Option.orElse (Some false)
                |> Option.get
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
        watch Queue.Cache handleMessage 5000
    } |> Async.RunSynchronously
    0
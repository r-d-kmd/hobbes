namespace Hobbes.Calculator.Services

open Hobbes.Web.Routing
open Hobbes.Web
open Hobbes.Web.Http
open FSharp.Data
open Hobbes.Shared.RawdataTypes

[<RouteArea ("/data", false)>]
module Data =
    
    let cache = Cache.Cache("transformation")
   
    let private getConfiguration configurationName =
            get (configurationName |> Some |> Configuration |> Configurations) Config.Parse

    let readFromCache (key : string list) = 
        (":",key)
        |> System.String.Join 
        |> cache.Get
        |> Option.bind (Cache.readData >> Some)

    let calc transformationName calculatedData =
        match get (transformationName |> Some |> Transformation |> Configurations) TransformationRecord.Parse with
        Success transformation ->
            Hobbes.FSharp.Compile.expressions transformation.Lines calculatedData
            |> Some
        | Error(e,m) -> 
            Log.errorf null "Error when retrieving transformation. staus: %d. Message: %s" e m
            None
        
    let rec readFromCacheOrCalculate (sourceKey : string) (keys : string list list) = 
        if keys |> List.isEmpty then 
            Cache.Cache(Http.UniformData).Get sourceKey
            |> Option.bind(Cache.readData
                >> Hobbes.FSharp.DataStructures.DataMatrix.fromRows
                >> Some
            )
        else
            match keys.Head |> readFromCache with
            Some d -> 
                d 
                |> Hobbes.FSharp.DataStructures.DataMatrix.fromRows 
                |> Some
            | None -> 
                let cacheKey = 
                    System.String.Join(":",keys)
                let prevTransformation = keys.Tail
                let currentTransformationName = keys.Head.Tail.Head
                readFromCacheOrCalculate sourceKey prevTransformation
                |> Option.bind(fun d ->
                    let result = calc currentTransformationName d
                    result
                    |> Option.iter(fun result ->
                        let json = 
                            result
                            |> Hobbes.FSharp.DataStructures.DataMatrix.toJson Hobbes.FSharp.DataStructures.Rows 
                        try
                            json |> Cache.createCacheRecord cacheKey
                        with e ->
                            Log.excf e "Failed to create cache record"
                            reraise()
                        |> cache.InsertOrUpdate
                    )
                    result
                )
                

    [<Get ("/calculate/%s")>]
    let calculate configurationName =
        match configurationName
              |> getConfiguration with
        Success config -> 
            let sourceKey = 
                config.Source.JsonValue.ToString()
                |> Cache.key
            let cacheKeys = 
                let trans = 
                    config.Transformations
                    |> List.ofSeq
                    |> List.rev
        
                trans.Tail
                |> List.fold(fun res trans ->
                   (trans::res.Head)::res
                ) [[trans.Head]]
                |> List.map(fun t ->
                    sourceKey::t
                )
            match readFromCacheOrCalculate sourceKey cacheKeys with
            Some d ->
                200,d.ToJson Hobbes.FSharp.DataStructures.JsonTableFormat.Csv
            | None -> 500,"calculation error"
        | Error(e,m) -> 
            e,m

    [<Get "/ping">]
    let ping () =
        200, "ping - UniformData"
namespace Hobbes.Calculator.Services

open Hobbes.Server.Routing
open Hobbes.Web
open Hobbes.Web.Http
open FSharp.Data
open Hobbes.Shared.RawdataTypes

[<RouteArea ("/data", false)>]
module Data =
    
    let cache = Cache.Cache("TransformationCache")

    let private getConfiguration configurationName =
            get "configurations" Config.Parse <| "/configuration/" + configurationName

    let readFromCache key = 
        (":",key)
        |> System.String.Join 
        |> cache.TryGet
        |> Cache.readData

    let calculate transformationName calculatedData =
        let transformation = get "configurations" TransformationRecord.Parse <| "/transformation/" + transformationName
        Hobbes.FSharp.Compile.expressions transformation.Lines calculatedData
        
    let rec readFromCacheOrCalculate (sourceKey : string) (keys : string list list) = 
        if keys |> List.isEmpty then 
            get "uniformdata" Cache.CacheRecord.Parse <| "/data/" + sourceKey
            |> Cache.readData
            |> Success
        else
            match keys.Head |> readFromCache with
            Some d -> 
                Success d
            | None -> 
                let prevTransformation = keys.Tail
                let currentTransformationName = keys.Head.Tail.Head
                readFromCacheOrCalculate sourceKey prevTransformation
                |> calculate currentTransformationName

    [<Post ("/calculate", true)>]
    let calculate configurationName =
        let config = 
            configurationName
            |> getConfiguration
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
            ) trans.Head
            |> List.map(fun t ->
                sourceKey::t
            )
        readFromCacheOrCalculate sourceKey cacheKeys

    [<Get "/ping">]
    let ping () =
        200, "ping - UniformData"
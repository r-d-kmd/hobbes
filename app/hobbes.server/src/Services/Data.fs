namespace Hobbes.Server.Services

open Hobbes.Web.Log
open Hobbes.Server.Db
open Hobbes.Web.Routing
open Hobbes.FSharp.DataStructures
open Hobbes.Helpers
open Hobbes.Server.Collectors

[<RouteArea "/data">]
module Data = 
    let cacheRevision confDoc = 
        sprintf "%s:%d" confDoc (System.DateTime.Now.Ticks) |> hash

                  

    let rec private data configurationName =
        let rec transformData searchKey prevTransformations (remaingTransformations : Transformations.TransformationRecord.Root list) calculatedData =
            
            match remaingTransformations with
            [] -> calculatedData
            | transformation::tail ->
                let transformedData =  
                    Hobbes.FSharp.Compile.expressions transformation.Lines calculatedData
                let transformations = prevTransformations@[transformation.Id]
                async {
                    debug "Caching transformation"
                    try
                        transformedData.ToJson(Column)
                        |> Cache.store transformations searchKey cacheRevision
                        |> ignore
                    with e ->
                        errorf e.StackTrace "Failed to cache transformation result. Message: %s" e.Message
                } |> Async.Start
                transformData searchKey transformations tail transformedData

        let readRawdata (configuration : DataConfiguration.Configuration) =
            async {
                let! uncachedTransformations, data =
                    Cache.findUncachedTransformationsAndCachedData configuration 

                let! transformations = 
                    Transformations.load uncachedTransformations

                let cachedTransformations = 
                    configuration.Transformations
                    |> Array.filter(fun t -> 
                        uncachedTransformations 
                        |> List.tryFind(fun t' -> t = t')   
                        |> Option.isNone
                    )

                let data =
                    match data with
                    None ->
                        debugf "Cache miss %s" configurationName
                        match configuration.SubConfigs |> Array.isEmpty with
                        true ->   
                            let rows  = 
                                configuration.JsonValue.ToString() |> Collector.read 
                            log "Transforming data into matrix"
                            rows
                            |> DataMatrix.fromRows
                        | false -> 
                            failwith "SubConfigs are not Implemented yet"
                    | Some data -> 
                        data
                        |> Seq.map(fun (columnName,values) -> 
                                columnName, values
                                            |> Seq.map(fun (i,v) -> Hobbes.Parsing.AST.KeyType.Create i, v)
                        ) 
                        |> DataMatrix.fromTable

                return transformations, data, cachedTransformations
            }

        let configuration = DataConfiguration.get configurationName

        async {
            
            let! transformations, cachedData, cachedTransformations = readRawdata configuration

            let transformedData = 
                match transformations with
                ts when ts |> Seq.isEmpty -> cachedData
                | transformations ->
                    transformData configuration.SearchKey (cachedTransformations |> List.ofArray) (transformations |> List.ofSeq) cachedData

            return transformedData
        } 

    [<Get ("/csv/%s")>]
    let csv configuration =  
        debugf "Getting csv for '%A'" configuration
        let data = data configuration |> Async.RunSynchronously
        let csv = data |> DataMatrix.toJson Csv
        debugf "CSV: %s" csv
        200, csv

    let invalidateCache statusCode body (configuration : DataConfiguration.Configuration) =
        try
            let cacheRevision = configuration.JsonValue.ToString() |> cacheRevision
            if statusCode >= 200 && statusCode < 300 then 
                debug "Invalidating cache"
                Cache.invalidateCache configuration cacheRevision |> Async.RunSynchronously
                debug "Recalculating"
                
                let configurations = DataConfiguration.configurationsBySource configuration.Source
                debugf "Found %d configurations to recalculate" (configurations |> Seq.length) 
                configurations
                |> Seq.iter(fun configuration -> 
                    debugf "Starting async calculation of %s" configuration
                    try
                        debugf "Getting data for configuration: %s" configuration
                        data configuration |> ignore //this forces the cache to be repopulated
                        if statusCode > 299 then 
                            errorf null "Failed to transform data. Status: %d" statusCode
                    with e ->
                        errorf e.StackTrace "Failed to transform data. Message: %s" e.Message
                )
                true, ""
            else
                let msg = sprintf "Syncronization failed. Message: %s" body
                errorf null "%s" msg
                false, msg
        with e ->
            false, e.Message

    [<Get ("/fSync/%s") >]
    let fSync configurationName =

        let configuration = DataConfiguration.get configurationName
        let status, _ = Admin.clearCache()
        async {
            let status,msg = configuration.JsonValue.ToString() |> Collector.sync
            if status >= 400 then
              errorf null "failed to sync. %d - %s" status msg
        } |> Async.Start
        status,"syncing"

    [<Get ("/sync/%s") >]
    let sync configurationName =
        fSync configurationName
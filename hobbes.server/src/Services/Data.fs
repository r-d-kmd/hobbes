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
        let rec transformData (configuration : DataConfiguration.Configuration) (transformations : Transformations.TransformationRecord.Root list) calculatedData =
            match transformations with
            [] -> calculatedData
            | transformation::tail ->
                let transformedData =  
                    Hobbes.FSharp.Compile.expressions transformation.Lines calculatedData

                let nextConfiguration = 
                    { configuration with
                        Transformations = configuration.Transformations@[transformation.Id]
                    } 

                async {
                    debug "Caching transformation"
                    try
                        transformedData.ToJson(Column)
                        |> Cache.store nextConfiguration cacheRevision
                        |> ignore
                    with e ->
                        errorf e.StackTrace "Failed to cache transformation result. Message: %s" e.Message
                } |> Async.Start
                transformData nextConfiguration tail transformedData

        let readRawdata configuration =
            async {
                let! uncachedTransformations, data =
                    Cache.findUncachedTransformationsAndCachedData configuration 

                let! transformations = 
                    Transformations.load uncachedTransformations

                let cachedTransformations = 
                    configuration.Transformations
                    |> List.filter(fun t -> 
                        uncachedTransformations 
                        |> List.tryFind(fun t' -> t = t')   
                        |> Option.isNone
                    )  
                let tempConfig = 
                    {
                        configuration with
                            Transformations = cachedTransformations
                    }
                let data =
                    match data with
                    None ->
                        debugf "Cache miss %s" configurationName
                        match configuration.Source with
                        DataConfiguration.AzureDevOps _ ->
                            log "Reading from raw"
                            match configuration.SubConfigs.IsEmpty with
                            | true ->   
                                let rows  = 
                                    configuration.Source.ConfDoc |> AzureDevOps.read 
                                log "Transforming data into matrix"
                                rows
                                |> DataMatrix.fromRows
                            | false -> failwith "SubConfigs are not Implemented yet"
                                       //tryGetSubConfigs configuration                                    
                        | _ -> failwith "Unknown source"
                    | Some data -> 
                        data
                        |> Seq.map(fun (columnName,values) -> 
                                columnName, values
                                            |> Seq.map(fun (i,v) -> Hobbes.Parsing.AST.KeyType.Create i, v)
                        ) 
                        |> DataMatrix.fromTable

                return transformations, data, tempConfig
            }

        let configuration = DataConfiguration.get configurationName

        async {
            
            let! transformations, cachedData, tempConfig = readRawdata configuration

            let transformedData = 
                match transformations with
                ts when ts |> Seq.isEmpty -> cachedData
                | transformations ->
                    transformData tempConfig (transformations |> List.ofSeq) cachedData

            return transformedData
        } 

    //TODO move housekeeping to collector
    let isSyncing configurationName =
        let configuration =
            try
                DataConfiguration.get configurationName |> Some
            with _ -> 
               None
        if configuration.IsNone then configurationName, false
        else
            let configuration = configuration.Value
            match configuration.Source with
            DataConfiguration.AzureDevOps(account, project)   ->
                let syncId = ("azure devops:" + project)
                log syncId
                let statusCode, stateDoc = AzureDevOps.getSyncState syncId
                if statusCode = 404 then
                    syncId,false
                else
                    let stateDoc = Cache.CacheRecord.Parse stateDoc
                    let state = stateDoc.State
                                |> Cache.SyncStatus.Parse
                    syncId ,state = Cache.SyncStatus.Started
            | _                                               ->
                let msg = sprintf "No collector found for: %s" configuration.Source.SourceName
                eprintfn "%s" msg
                "No collector found", false


    [<Get ("/csv/%s")>]
    let csv configuration =  
        let syncId, syncing = isSyncing configuration
        if syncing then 
            489, sprintf "Sync currently running for %s, please wait." syncId
        else
            debugf "Getting csv for '%A'" configuration
            let data = data configuration |> Async.RunSynchronously
            let csv = data |> DataMatrix.toJson Csv
            printfn "CSV: %s" csv
            200, csv


    [<Get ("/raw/%s") >]
    let getRaw id =
        AzureDevOps.getRaw id

    let invalidateCache statusCode body (configuration : DataConfiguration.Configuration) =
        try
            let cacheRevision = configuration.Source.ConfDoc |> cacheRevision
            if statusCode >= 200 && statusCode < 300 then 
                debug "Invalidating cache"
                Cache.invalidateCache configuration.Source cacheRevision |> Async.RunSynchronously
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
                eprintfn "%s" msg
                false, msg
        with e ->
            false, e.Message

    [<Get ("/fSync/%s") >]
    let fSync configurationName =

        let configuration = DataConfiguration.get configurationName
        let status, body = Admin.clearCache()
        async {
            configuration.Source.ConfDoc |> AzureDevOps.sync  |> ignore
        } |> Async.Start
        status,body

    [<Get ("/sync/%s") >]
    let sync configurationName =
        let syncId, syncing = isSyncing configurationName
        log syncId
        logf "%A" syncing
        if syncing then 
            489, sprintf "Sync currently running for %s, please wait." syncId
        else 
            fSync configurationName
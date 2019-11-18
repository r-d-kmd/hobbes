namespace Hobbes.Server.Services

open Hobbes.Server.Db.Database
open Hobbes.Server.Db.Log
open Hobbes.Server.Db
open Routing
open Hobbes.FSharp.DataStructures

[<RouteArea "/data">]
module Data = 
    let cacheRevision (source : DataConfiguration.DataSource) = 
        sprintf "%s:%s:%d" source.SourceName source.ProjectName (System.DateTime.Now.Ticks) |> hash

    let private data configurationName =
        let configuration = DataConfiguration.get configurationName
        let uncachedTransformations, data =
            Cache.findUncachedTransformations configuration

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

        let transformations = 
            Transformations.load uncachedTransformations
            
        let cachedData = 
            match data with
            None ->
                debugf "Cache miss %s" configurationName
                match configuration.Source with
                DataConfiguration.AzureDevOps(account,projectName) ->
                    let rows  = 
                        Hobbes.Server.Readers.AzureDevOps.readCached (account,projectName)
                        |> List.ofSeq
                    rows
                    |> DataMatrix.fromRows
                | _ -> failwith "Unknown source"
            | Some data -> 
                data
                |> Seq.map(fun (columnName,values) -> 
                        columnName, values
                                    |> Seq.map(fun (i,v) -> Hobbes.Parsing.AST.KeyType.Create i, v)
                ) 
                |> DataMatrix.fromTable
        let cacheRevision = cacheRevision configuration.Source
        let transformedData = 
            match transformations with
            ts when ts |> Seq.isEmpty -> cachedData
            | transformations ->
                transformations
                |> Seq.fold(fun (calculatedData, (tempConfig : DataConfiguration.Configuration)) transformation -> 
                    let transformedData =  
                        Hobbes.FSharp.Compile.expressions transformation.Lines calculatedData
                    let tempConfig = 
                        { tempConfig with
                            Transformations = tempConfig.Transformations@[transformation.Id]
                        } 
                    async {
                        printfn "Caching transformation"
                        try
                            transformedData.ToJson(Column)
                            |> Cache.store tempConfig cacheRevision
                            |> ignore
                        with e ->
                            eprintfn "Failed to cache transformation result. Message: %s" e.Message
                    } |> Async.Start
                    transformedData, tempConfig
                )  (cachedData, tempConfig)
                |> fst
        transformedData 

    [<Get ("/csv/%s")>]
    let csv configuration = 
        debugf "Getting csv for '%A'" configuration
        let data = data configuration
        200,data 
        |> DataMatrix.toJson Csv

    [<Get "/raw/%s" >]
    let getRaw id =
        Rawdata.get id

    [<Get "/sync/%s" >]
    let sync configurationName =
        let azureToken = env "AZURE_TOKEN"
        let configuration = DataConfiguration.get configurationName
        let cacheRevision = cacheRevision configuration.Source
        let syncId = Rawdata.createSyncStateDocument cacheRevision configuration.Source
        async {
            try
                match configuration.Source with
                DataConfiguration.AzureDevOps(account,projectName) ->
                  
                    let statusCode,body = Hobbes.Server.Readers.AzureDevOps.sync azureToken (account,projectName) cacheRevision
                    logf "Sync finised with statusCode %d and result %s" statusCode body
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
                        Rawdata.setSyncCompleted cacheRevision configuration.Source 
                    else
                        let msg = sprintf "Syncronization failed. Message: %s" body
                        eprintfn "%s" msg
                        Rawdata.setSyncFailed msg cacheRevision configuration.Source  
                | _ -> 
                    let msg = sprintf "No collector found for: %s" configuration.Source.SourceName
                    eprintfn "%s" msg
                    Rawdata.setSyncFailed msg cacheRevision  configuration.Source 
            with e ->
                Rawdata.setSyncFailed e.Message cacheRevision configuration.Source
        } |> Async.Start
        200, syncId
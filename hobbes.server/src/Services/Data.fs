namespace Hobbes.Server.Services

open Hobbes.Web.Log
open Hobbes.Server.Db
open Hobbes.Server.Routing
open Hobbes.FSharp.DataStructures
open Hobbes.Helpers

[<RouteArea "/data">]
module Data = 
    let cacheRevision (source : DataConfiguration.DataSource) = 
        sprintf "%s:%s:%d" source.SourceName source.ProjectName (System.DateTime.Now.Ticks) |> hash
    
    let private data configurationName =
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
                        DataConfiguration.AzureDevOps(account,projectName) ->
                            log "Reading from raw"
                            let rows  = 
                                Hobbes.Server.Readers.AzureDevOps.readCached account projectName
                                |> List.ofSeq
                            log "Transforming data into matrix"
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

    [<Get ("/csv/%s")>]
    let csv configuration = 
        async {
            debugf "Getting csv for '%A'" configuration
            let! data = data configuration
            return 200, data |> DataMatrix.toJson Csv
        } |> Async.RunSynchronously

    [<Get ("/raw/%s") >]
    let getRaw id =
        Rawdata.get id

    let sync configurationName azureToken =
        let configuration = DataConfiguration.get configurationName
        let cacheRevision = cacheRevision configuration.Source

        let syncId = Rawdata.createSyncStateDocument cacheRevision configuration.Source
        async {
            try
                match configuration.Source with
                DataConfiguration.AzureDevOps(account,projectName) ->
                    let statusCode,body = Hobbes.Server.Readers.AzureDevOps.sync azureToken (account,projectName)
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
        
    [<Get ("/sync/%s") >]
    let syncronize configurationName = 
        let configuration = DataConfiguration.get configurationName
        configuration.Source 
        |> Admin.clearProject  
        |> ignore
        Admin.clearCache() |> ignore
        let token =
            match configuration.Source with
            DataConfiguration.DataSource.AzureDevOps(account,_)  ->
                let varName = 
                    sprintf "AZURE_TOKEN_%s" <| account.ToUpper().Replace("-","_")
                env varName null
            | source -> failwithf "Not supported. %A"source
        sync configurationName token
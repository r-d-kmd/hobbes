namespace Hobbes.Server.Services

open Hobbes.Web.Log
open Hobbes.Server.Db
open Hobbes.Server.Routing
open Hobbes.FSharp.DataStructures
open Hobbes.Helpers
open Hobbes.Server.Collectors

[<RouteArea "/data">]
module Data = 
    let cacheRevision (source : DataConfiguration.DataSource) = 
        sprintf "%s:%s:%d" source.SourceName source.ProjectName (System.DateTime.Now.Ticks) |> hash
                                                            
    let rec private data configurationName =

        (*let tryGetSubConfigs (configuration : DataConfiguration.Configuration) =
            let rec aux (configs : string list) (matrix : IDataMatrix) =
                match configs with 
                | []    -> log "Combined all subConfigs"
                           matrix
                | x::xs -> logf "Retrieving subConfig: %s" x
                           aux xs (matrix.Combine (Async.RunSynchronously (data x)))

            if configuration.SubConfigs.Length <= 1 
            then failwith "Less than 2 configurations in subconfigs is not legal"
            else
                let c, cs = configuration.SubConfigs |> List.head, configuration.SubConfigs |> List.tail
                logf "Retrieving subConfig: %s" c
                let matrix = Async.RunSynchronously (data c)
                aux cs matrix      *)


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
                            match configuration.SubConfigs.IsEmpty with
                            | true ->   let rows  = 
                                            AzureDevOps.readCached account projectName
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

            return (transformedData)
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
        AzureDevOps.getRaw id

    let invalidateCache statusCode body (configuration : DataConfiguration.Configuration) =
        try
            let cacheRevision = cacheRevision configuration.Source
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

    [<Get ("/sync/%s") >]
    let synchronize configurationName =
        let configuration = DataConfiguration.get configurationName
        Admin.clearCache() |> ignore
        
        let revision = cacheRevision configuration.Source
        let statusCode, syncId =
            match configuration.Source with
            DataConfiguration.AzureDevOps(account, project)   ->
                AzureDevOps.createSyncDoc account project revision
            | _                                               ->
                let msg = sprintf "No collector found for: %s" configuration.Source.SourceName
                eprintfn "%s" msg
                404, msg        
        async {
            match configuration.Source with
            DataConfiguration.AzureDevOps(account,project) ->
                let statusCode, body = AzureDevOps.sync account project  
                let completed, msg = invalidateCache statusCode body configuration
                if completed 
                then AzureDevOps.setSyncCompleted account project revision
                else AzureDevOps.setSyncFailed account project revision msg
                |> ignore                 
            | _ -> 
                let msg = sprintf "No collector found for: %s" configuration.Source.SourceName
                eprintfn "%s" msg
        } |> Async.Start                    
        statusCode, syncId
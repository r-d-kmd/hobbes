module Implementation

open Hobbes.FSharp.DataStructures
open Hobbes.Server.Db.Database
open Hobbes.Server.Db.Log
open Hobbes.Server.Db
open FSharp.Data
open Hobbes.Server.Security
open Routing


[<Get "/sync/%s">] 
let getSyncState syncId =
    200, (Rawdata.getState syncId).ToString()
      
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
            printfn "Cache miss %s" configurationName
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
    200,transformedData 

[<Get ("/csv/%s")>]
let csv configuration = 
    printfn "Getting csv for '%A'" configuration
    let status, data = data configuration
    status,data 
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
                Log.logf "Sync finised with statusCode %d and result %s" statusCode body
                if statusCode >= 200 && statusCode < 300 then 
                    Log.debug "Invalidating cache"
                    Cache.invalidateCache configuration.Source cacheRevision |> Async.RunSynchronously
                    Log.debug "Recalculating"
                    
                    let configurations = DataConfiguration.configurationsBySource configuration.Source
                    Log.debugf "Found %d configurations to recalculate" (configurations |> Seq.length) 
                    configurations
                    |> Seq.iter(fun configuration -> 
                        Log.debugf "Starting async calculation of %s" configuration
                        try
                            Log.debugf "Getting data for configuration: %s" configuration
                            let statusCode, _ = data configuration
                            if statusCode > 299 then 
                                Log.errorf null "Failed to transform data. Status: %d" statusCode
                        with e ->
                            Log.errorf e.StackTrace "Failed to transform data. Message: %s" e.Message
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

[<Get "/key/%s" >] 
let key token =
    let user = 
        token
        |> tryParseUser
        |> Option.bind(fun (user,token) -> 
              let userId = sprintf "org.couchdb.user:%s" user
              match users.TryGet userId with
              None  ->
                Log.logf  "Didn't find user. %s" userId
                let userRecord = 
                    sprintf """{
                        "_id" : "%s",
                      "name": "%s",
                      "type": "user",
                      "roles": [],
                      "password": "%s"
                    }""" userId user token
                userRecord
                |> users.Post
                |> ignore
                users.FilterByKeys [userId]
                |> Seq.head
                |> Some
              | s -> s 
        )

    match user with
    None ->
        eprintfn "No user token. Tried with %s" token 
        403,"Unauthorized"
    | Some (user) ->
        printfn "Creating api key for %s " user.Name
        let key = createToken user
        200,key

let private request user pwd httpMethod body url  =
    let headers =
        [
            HttpRequestHeaders.BasicAuth user pwd
            HttpRequestHeaders.ContentType HttpContentTypes.Json
        ]
    match body with
    None -> 
        Http.Request(url,
            httpMethod = httpMethod, 
            silentHttpErrors = true,
            headers = headers
        )
    | Some body ->
        Http.Request(url,
            httpMethod = httpMethod, 
            silentHttpErrors = true, 
            body = TextRequest body,
            headers = headers
        )

[<Get "/ping" >] 
let ping() = 
    let app = Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application
    
    200,sprintf """{"appVersion": "%s", "runtimeFramework" : "%s", "appName" : "%s"}""" app.ApplicationVersion app.RuntimeFramework.FullName app.ApplicationName

let delete databaseName =
    couch.Delete databaseName
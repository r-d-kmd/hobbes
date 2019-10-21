module Implementation

open Hobbes.FSharp.DataStructures
open Database
open Hobbes.Server.Db
open FSharp.Data
open Hobbes.Server.Security

let getSyncState syncId =
    Rawdata.getState syncId
    
let data configurationName =
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
            Rawdata.list configuration.Source
        | Some data -> data
   
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
                        |> Cache.store tempConfig 
                        |> ignore
                    with e ->
                        eprintfn "Failed to cache transformation result. Message: %s" e.Message
                } |> Async.Start
                transformedData, tempConfig
            )  (cachedData, tempConfig)
            |> fst

    200,transformedData |> DataMatrix.ToJson Csv
       
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
let private azureFields = 
    [
     "ChangedDate"
     "WorkITemId"
     "WorkItemRevisionSK"
     "WorkItemType"
     "State"
     "StateCategory"
     "LeadTimeDays"
     "CycleTimeDays"
     "Iteration"
    ]
let private getInitialUrl (source : DataConfiguration.DataSource) =
    let initialUrl = 
        let selectedFields = 
           (",", azureFields) |> System.String.Join
        sprintf "https://analytics.dev.azure.com/kmddk/%s/_odata/v2.0/WorkItemRevisions?$expand=Iteration&$select=%s&$filter=IsLastRevisionOfDay%%20eq%%20true%%20and%%20WorkItemRevisionSK%%20gt%%20%d" source.ProjectName selectedFields
    
    match  source |> Rawdata.tryLatestId with
    Some workItemRevisionId -> 
        initialUrl workItemRevisionId
    | None -> initialUrl 0

let invalidateCache() = 
    eprintfn "Missing implementation"

let sync pat configurationName =
    let configuration = DataConfiguration.get configurationName
    let syncId = Rawdata.createSyncStateDocument configuration.Source
    async {
        match configuration.Source with
        DataConfiguration.AzureDevOps projectName ->
            
            let rec _sync (url : string) = 
                let resp = 
                    url
                    |> request pat pat "GET" None
                if resp.StatusCode = 200 then
                    let record = 
                        match resp.Body with
                        Text body ->
                            body
                            |> AzureDevOpsAnalyticsRecord.Parse
                            |> Some
                        | _ -> 
                            None
                    match record with
                    Some record ->
                        let data = record.JsonValue.ToString JsonSaveOptions.DisableFormatting
                        let responseText = Rawdata.InsertOrUpdate data
                        if System.String.IsNullOrWhiteSpace(record.OdataNextLink) |> not then
                            printfn "Countinuing sync"
                            printfn "%s" record.OdataNextLink
                            _sync record.OdataNextLink
                        else 
                            200, responseText
                    | None -> 500, "Couldn't parse record"
                else 
                    resp.StatusCode, (match resp.Body with Text t -> t | _ -> "")
            let statusCode, body = 
                projectName
                |> DataConfiguration.AzureDevOps
                |> getInitialUrl
                |> _sync
            if statusCode >= 200 && statusCode < 300 then 
                async {
                    invalidateCache()
                    //TODO: this should loop through all configurations that uses this particular source
                    data configurationName |> ignore
                    Rawdata.setSyncCompleted configuration.Source
                } |> Async.Start
            else
                eprintfn "Syncronization failed. Message: %s" body
                Rawdata.setSyncFailed configuration.Source    
        | _ -> 
            eprintfn "No collector found for: %s" configuration.Source.SourceName
            Rawdata.setSyncFailed configuration.Source
    } |> Async.Start
    200, syncId
    

let key token =
    let user = 
        token
        |> tryParseUser
        |> Option.bind(fun (user,token) -> 
              let userId = sprintf "org.couchdb.user%%3A%s" user
              match users.TryGet userId with
              None ->
                printfn "Didn't find user. %s" userId
                let userRecord = 
                    sprintf """{
                      "name": "%s",
                      "type": "user",
                      "roles": [],
                      "password": "%s"
                    }""" user token
                (userId, userRecord)
                |> users.Put
                |> ignore
                users.Get userId
                |> Some
              | s -> s
        )

    match user with
    None ->
        eprintfn "No user token. Tried with %s" token 
        403,"Unauthorized"
    | Some (user) ->
        printfn "Creating api key for %s" user.Name
        let key = createToken user
        200,key

let storeTransformations doc = 
    try
        Transformations.store doc |> ignore
        200,"ok"
    with _ -> 
        500,"internal server error"

let listConfigurations() = 
    DataConfiguration.list()

let listCache() = 
    Cache.list()

let listTransformations() = 
    Transformations.list()

let storeConfigurations doc = 
    try
        DataConfiguration.store doc |> ignore
        200,"ok"
    with _ -> 
        500,"internal server error"

let private uploadDesignDocument (storeHandle, (hashHandle : string -> int option), file) =
    async {
        let! doc = System.IO.File.ReadAllTextAsync file |> Async.AwaitTask
        if System.String.IsNullOrWhiteSpace (CouchDoc.Parse doc).Rev |> not then failwithf "Initialization documents shouldn't have _revs %s" file
        let designDocName = System.IO.Path.GetFileNameWithoutExtension file
        let oldHash = designDocName
                      |> hashHandle
        let newDoc = (doc 
                       |> String.filter(not << System.Char.IsWhiteSpace))
        let newHash = newDoc.GetHashCode System.StringComparison.Ordinal //TODO "Hashcode changes when restarting program???"                  

        return if oldHash.IsNone || oldHash.Value <> newHash then
                    let id = sprintf "%s_hash" designDocName
                    sprintf """{"_id": %A, "hash":%i }"""  id newHash
                    |> storeHandle 
                    |> ignore
                    storeHandle doc
                else 
                    ""                                                        
    }

//test if db is alive
let ping() = 
    couch.Get "_all_dbs"
    200,"pong"
    
let initDb () =
    let dbs = 
        [
            "transformations", (Transformations.store, Transformations.tryGetHash)
            "rawdata", (Rawdata.InsertOrUpdate, Rawdata.tryGetHash)
            "configurations", (DataConfiguration.store, DataConfiguration.tryGetHash)
            "cache", (Cache.InsertOrUpdate, Cache.tryGetHash)
        ] 
    let systemDbs = 
        [
            "_replicator"
            "_global_changes"
            "_users"
        ]
    let errorCode = 
        (dbs |> List.map fst)@systemDbs
        |> List.map (fun n -> couch.TryPut(n, "").StatusCode)
        |> List.tryFind (fun sc -> ((sc >= 200 && sc < 300) || (sc = 412)) |> not)
    (match errorCode with
     Some errorCode ->
        "error in creating dbs", errorCode
     | None ->
        let dbMap = dbs |> Map.ofList
        try
            let documentDir = "db/documents"
            if System.IO.Directory.Exists "db/documents" |> not then failwith "Document folder not found"
            (System.IO.Directory.EnumerateDirectories(documentDir)
            |> Seq.collect(fun dir -> 
                System.IO.Directory.EnumerateFiles(dir,"*.json")
                |> Seq.map(fun f -> 
                    let dbName = System.IO.Path.GetFileName dir
                    let handles = dbMap 
                                  |> Map.find dbName
                    fst handles, snd handles, f) 
            ) |> Seq.map uploadDesignDocument
            |> Async.Parallel
            |> Async.RunSynchronously).[0], 200
        with e ->
            eprintfn "Error in init %s" e.Message
            e.Message, 500
    )
module Implementation

open Hobbes.FSharp.DataStructures
open Hobbes.Server.Db.Database
open Hobbes.Server.Db.Log
open Hobbes.Server.Db
open FSharp.Data
open Hobbes.Server.Security

let getSyncState syncId =
    200, (Rawdata.getState syncId).ToString()

let private hash (input : string) =
        use md5Hash = System.Security.Cryptography.MD5.Create()
        let data = md5Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input))
        let sBuilder = System.Text.StringBuilder()
        (data
        |> Seq.fold(fun (sBuilder : System.Text.StringBuilder) d ->
                sBuilder.Append(d.ToString("x2"))
        ) sBuilder).ToString()        

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
            DataConfiguration.AzureDevOps projectName ->
                let rows  = 
                    Hobbes.Server.Readers.AzureDevOps.readCached projectName
                    |> List.ofSeq
                rows
                |> DataMatrix.fromRows
            | _ -> failwith "Unknown source"
        | Some data -> data
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

let csv configuration = 
    let status, data = data configuration
    status,data 
           |> DataMatrix.toJson Csv

let setting (area, setting) =
    200,couch.Get [
                "_node"
                "_local"
                "_config"
                area
                setting
    ]

let configure (area, setting, value) =
    200,couch.Put ([
                "_node"
                "_local"
                "_config"
                area
                setting
    ],value)

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

let sync azureToken configurationName =
    let configuration = DataConfiguration.get configurationName
    let cacheRevision = cacheRevision configuration.Source
    let syncId = Rawdata.createSyncStateDocument cacheRevision configuration.Source
    async {
        try
            match configuration.Source with
            DataConfiguration.AzureDevOps projectName ->
              
                let statusCode,body = Hobbes.Server.Readers.AzureDevOps.sync azureToken projectName cacheRevision
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

let storeTransformations doc = 
    try
        Transformations.store doc |> ignore
        200,"ok"
    with _ -> 
        500,"internal server error"

let formatDBList name list =
    let stringList = list
                     |> Seq.map (sprintf "%A")
    let body = sprintf """{"%s" : [%s]}""" <| name <| System.String.Join(",", stringList)
    200, body    

let listConfigurations() = 
    DataConfiguration.list() |> formatDBList "configurations"
let listCache() = 
    Cache.list() |> formatDBList "cache"
let listTransformations() = 
    Transformations.list() |> formatDBList "transformations"

let listRawdata() = 
    Rawdata.list() |> formatDBList "rawdata"           

let listLog() = 
    Log.list()
    |> Seq.map LogRecord.Parse
    |> Seq.filter(fun record -> record.Type <> "requestTiming")
    |> Seq.sortByDescending(fun record -> record.Timestamp)
    |> Seq.map(fun logRecord ->
        let st = 
            match logRecord.Stacktrace with
            None -> ""
            | Some st -> sprintf "\n%s" st
        sprintf "%s - [%s] %s %s" (logRecord.Timestamp.ToString()) logRecord.Type logRecord.Message st
    ) |> formatDBList "logEntries"

let storeConfigurations doc = 
    try
        DataConfiguration.store doc |> ignore
        200,"ok"
    with _ -> 
        500,"internal server error"

let private uploadDesignDocument (db : Database<CouchDoc.Root>, file) =
    
    async {
        let! doc = System.IO.File.ReadAllTextAsync file |> Async.AwaitTask
        if System.String.IsNullOrWhiteSpace (CouchDoc.Parse doc).Rev |> not then failwithf "Initialization documents shouldn't have _revs %s" file
        let designDocName = System.IO.Path.GetFileNameWithoutExtension file
        let oldHash = designDocName
                      |> db.TryGetHash
        let newDoc = (doc 
                       |> String.filter(not << System.Char.IsWhiteSpace))
                       
        let newHash = hash newDoc                

        let res = 
            if oldHash.IsNone || oldHash.Value <> newHash then
                let id = sprintf "%s_hash" designDocName
                sprintf """{"_id": %A, "hash":%A }"""  id newHash
                |> db.InsertOrUpdate 
                |> ignore
                db.InsertOrUpdate doc
            else 
                ""
        db.CompactAndClean()
        return res
    }

//return application info as a sign that all is well
let ping() = 
    let app = Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application
    
    200,sprintf """{"appVersion": "%s", "runtimeFramework" : "%s", "appName" : "%s"}""" app.ApplicationVersion app.RuntimeFramework.FullName app.ApplicationName

let delete databaseName =
    couch.Delete databaseName
    
[<Literal>]
let private SettingsPath = """./db/documents/settings.json"""
type private Settings = FSharp.Data.JsonProvider<SettingsPath>

let initDb () =
    let configurationBase = "_node/_local/_config"
    let settingsDb = Database(configurationBase,id, Log.loggerInstance)
    Settings.Load SettingsPath
    |> Array.iter(fun setting ->
         let value =
            setting.Value.Number
            |> Option.bind(string >> Some)
            |> Option.orElse(setting.Value.String)
            |> Option.get
            |> sprintf "%A"
         settingsDb.Put([
                          setting.Area
                          setting.Name
                        ], value) |> printfn "Old value: %s"
    )
    let dbs = 
        [
            "transformations"
            "rawdata"
            "configurations"
            "cache"
            "log"
        ] 
    let systemDbs = 
        [
            "_replicator"
            "_global_changes"
            "_users"
        ]
    let errorCode = 
        dbs@systemDbs
        |> List.map (fun n -> couch.TryPut(n, "") |> fst)
        |> List.tryFind (fun sc -> ((sc >= 200 && sc < 300) || (sc = 412)) |> not)
    match errorCode with
     Some errorCode ->
        let msg = "INIT: error in creating dbs"
        Log.error null msg
        errorCode, msg
     | None ->
        try
            let documentDir = "db/documents"
            if System.IO.Directory.Exists documentDir |> not then failwith "Document folder not found"
            (System.IO.Directory.EnumerateDirectories(documentDir)
            |> Seq.collect(fun dir -> 
                System.IO.Directory.EnumerateFiles(dir,"*.json")
                |> Seq.map(fun f -> 
                    let dbName = System.IO.Path.GetFileName dir
                    let db = Database(dbName, CouchDoc.Parse, ignoreLogging)
                    let insertOrUpdate =
                        db.InsertOrUpdate
                    let tryGetHash = db.TryGetHash
                    db, f
                ) 
            ) |> Seq.map uploadDesignDocument
            |> Async.Parallel
            |> Async.RunSynchronously) |> ignore

            let msg = "Init completed"
            Log.log msg
            200,msg
        with e ->
            Log.errorf e.StackTrace "Error in init: %s" e.Message
            500,e.Message
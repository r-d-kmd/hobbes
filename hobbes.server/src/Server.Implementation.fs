module Implementation

open Hobbes.FSharp.DataStructures
open Database
open Hobbes.Server.Db
open FSharp.Data
open Hobbes.Server.Security

let private invalidateCache (conf : DataConfiguration.Configuration) =
    let id = conf.Source.SourceName + ":" + conf.Source.ProjectName
    cache.Views.["srcproj"].List(IdRecord.Parse, startKey = id, endKey = id)
    |> Array.map(fun cache -> Cache.delete cache.Id)

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

    let cachekeyPrefix = 
        {
            configuration with
                Transformations = cachedTransformations
        }

    let transformations = 
        Transformations.load uncachedTransformations
        
    let data = 
        match data with
        None -> 
            printfn "Cache miss %s" configurationName
            let datasetKey = [configuration.Source.SourceName;configuration.Source.ProjectName]
            Rawdata.list datasetKey
        | Some data -> data
   
    let data = 
        transformations
        |> Seq.fold(fun (data, (prefix : DataConfiguration.Configuration)) transformation -> 
            let data =  Hobbes.FSharp.Compile.expressions transformation.Lines data
            let prefix = 
                { prefix with
                    Transformations = prefix.Transformations@[transformation.Id]
                } 
            async {
                data.ToJson(Column)
                |> Cache.store prefix 
                |> ignore
            } |> Async.Start
            data, prefix
        )  (data, cachekeyPrefix)
        |> fst
        |> DataMatrix.ToJson Csv

    200,data
       
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
let private clearTempAzureDataAndGetInitialUrl (source : DataConfiguration.DataSource) =
    let initialUrl = 
        let selectedFields = 
           (",", azureFields) |> System.String.Join
        sprintf "https://analytics.dev.azure.com/kmddk/%s/_odata/v2.0/WorkItemRevisions?$expand=Iteration&$select=%s&$filter=IsLastRevisionOfDay%%20eq%%20true%%20and%%20WorkItemRevisionSK%%20gt%%20%d" source.ProjectName selectedFields
    
    let latestId = 
        [source.SourceName;source.ProjectName]
        |> Rawdata.tryLatestId
    match latestId with
    Some workItemRevisionId -> 
        initialUrl workItemRevisionId
    | None -> initialUrl 0


let sync pat configurationName =
    let configuration = DataConfiguration.get configurationName
    
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
                    let responseText = Rawdata.store configuration.Source.SourceName configuration.Source.ProjectName url data
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
            |> clearTempAzureDataAndGetInitialUrl
            |> _sync
        async {
            data configurationName |> ignore
        } |> Async.Start
        statusCode, body
    | _ -> 
        404,"No reader found" 
    

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

let putDocument (db : IDatabase) doc =
    let id = (doc |> CouchDoc.Parse).Id
    let rev = db.TryGetRev id
    let res = if rev.IsNone then db.TryPut(id, doc, None)
                            else db.TryPut(id, doc, Some rev.Value)      
    res.StatusCode, ""

let private uploadDesignDocument (db,file) =
    async {
        let! doc = System.IO.File.ReadAllTextAsync file |> Async.AwaitTask
        return putDocument db doc |> fst
    }

//test if db is alive
let ping() = 
    couch.Get "_all_dbs"
    200,"pong"
    
let initDb () =
    let dbs  = 
        [
            "transformations", transformations :> Database.IDatabase
            "rawdata", rawdata :> Database.IDatabase
            "configurations", configurations :> Database.IDatabase
            "cache", cache :> Database.IDatabase
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
        |> List.tryFind (fun sc -> sc < 200 || (400 >= sc && sc <> 412))
    (match errorCode with
     Some errorCode ->
        errorCode
     | None ->
        let dbMap = dbs |> Map.ofList
        let errorCode = 
            async {
                let! results = 
                    System.IO.Directory.EnumerateDirectories("db/documents")
                    |> Seq.collect(fun dir -> 
                        System.IO.Directory.EnumerateFiles(dir,"*.json")
                        |> Seq.map(fun f -> 
                            let dbName = System.IO.Path.GetFileName dir
                            dbMap 
                            |> Map.find dbName,f) 
                    ) |> Seq.map uploadDesignDocument
                    |> Async.Parallel
                return 
                    results
                    |> Array.tryFind (fun sc -> sc < 200 || (400 >= sc && sc <> 412))
            } |> Async.RunSynchronously

        match errorCode with
        None -> 200
        | Some errorCode -> errorCode), ""
module AzureDevopsCollector.Implementation

open Hobbes.Server.Db.Database
open Hobbes.Server.Db.Log
open Hobbes.Server.Db

let ping () =
    200, "dingeling"

let getSyncState syncId =
    200, (Rawdata.getState syncId).ToString()

let cacheRevision (source : DataConfiguration.DataSource) = 
        sprintf "%s:%s:%d" source.SourceName source.ProjectName (System.DateTime.Now.Ticks) |> hash

let sync configurationName =
        let azureToken = env "AZURE_TOKEN" null
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
                                //data configuration |> ignore //this forces the cache to be repopulated
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

let initDb () =
    let dbs = 
        [
            "transformations"
            "rawdata"
            "uniform"
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
        error null msg
        errorCode, msg
     | None ->
        try
            let documentDir = "./documents"
            if System.IO.Directory.Exists documentDir |> not then failwith "Document folder not found"
            (System.IO.Directory.EnumerateDirectories(documentDir)
            |> Seq.collect(fun dir -> 
                let dbName = System.IO.Path.GetFileName dir
                System.IO.Directory.EnumerateFiles(dir,"*.json") 
                |> Seq.filter (fun _ -> List.exists (fun db -> db.Equals(dbName)) dbs)
                |> Seq.map (fun f -> 
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
            log msg
            200,msg
        with e ->
            errorf e.StackTrace "Error in init: %s" e.Message
            500,e.Message
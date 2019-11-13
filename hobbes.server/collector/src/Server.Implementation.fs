module Collector.Implementation
open Hobbes.Collector.Db.Database
open Hobbes.Collector.Db

let ping () =
    200, "pong"

let getSyncState syncId =
    //200, (Rawdata.getState syncId).ToString()
    500, "not implemented yet"

let sync azureToken configurationName =
  (*let configuration = DataConfiguration.get configurationName
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
    200, syncId*)
    500, "not implemented yet"

let csv configuration = 
  (*let status, data = data configuration
    status,data 
           |> DataMatrix.toJson Csv *)
    500, "not implemented yet"

let getRaw id =
    Raw.get id

let private hash (input : string) =
        use md5Hash = System.Security.Cryptography.MD5.Create()
        let data = md5Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input))
        let sBuilder = System.Text.StringBuilder()
        (data
        |> Seq.fold(fun (sBuilder : System.Text.StringBuilder) d ->
                sBuilder.Append(d.ToString("x2"))
        ) sBuilder).ToString()     

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
            "raw"
            "uniform"
            "transformations"
            "log"
        ]
    let systemDbs =
        [
            "_replicator"
            "_global_changes"
            "_users"
        ]
    let errorCode =
        dbs @ systemDbs
        |> List.map (fun n -> couch.TryPut(n, "") |> fst)
        |> List.tryFind (fun sc -> ((sc >= 200 && sc < 300) || sc = 412) |> not)
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
            |> Seq.collect (fun dir ->
                System.IO.Directory.EnumerateFiles(dir, "*.json")
                |> Seq.map(fun f ->
                    let dbName = System.IO.Path.GetFileName dir
                    let db = Database(dbName, CouchDoc.Parse, Log.ignoreLogging)
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
            500, e.Message                   
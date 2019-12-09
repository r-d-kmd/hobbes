namespace Collector.AzureDevOps

open Hobbes.Web.Database
open Hobbes.Server.Db
open Hobbes.Server.Log
open Hobbes.Web
open Hobbes.Server.Routing
open Hobbes.Server.Readers
open Hobbes.Helpers

[<RouteArea ("/", false)>]
module Root =

    let cacheRevision (source : DataConfiguration.DataSource) = 
        sprintf "%s:%s:%d" source.SourceName source.ProjectName (System.DateTime.Now.Ticks) |> hash

    let synchronize source token =
        let cacheRevision = cacheRevision source

        let syncId = Rawdata.createSyncStateDocument cacheRevision source
        async {
            try
                match source with
                DataConfiguration.AzureDevOps(account,projectName) ->
                    if Rawdata.exists() |> not then Rawdata.init |> ignore else () //TODO, rename Rawdata to azureRaw
                    let statusCode,body = AzureDevOps.sync token (account,projectName)
                    Log.logf "Sync finised with statusCode %d and result %s" statusCode body
                    if statusCode >= 200 && statusCode < 300 then 
                        //TODO: if caching failed in server, we shouldn't setSyncCompleted?
                        Rawdata.setSyncCompleted cacheRevision source 
                    else
                        let msg = sprintf "Syncronization failed. Message: %s" body
                        eprintfn "%s" msg
                        Rawdata.setSyncFailed msg cacheRevision source  
                | source -> 
                    let msg = sprintf "No collector found for: %s" source.SourceName
                    eprintfn "%s" msg
                    Rawdata.setSyncFailed msg cacheRevision source
            with e ->
                Rawdata.setSyncFailed e.Message cacheRevision source
        } |> Async.Start
        200, syncId

    [<Get "/ping">]
    let ping () =
        200, "ping"

    [<Get ("/raw/%s/%s/%s")>]
    let raw ((source : string), account, project) : int * string =
        let raw = 
            match source.ToLower() with
            "azure" ->
                AzureDevOps.readCached account project |> string //TODO: Turn into JSON
            | _ -> 
                "Source not supported"            
        404, "Not implemented yet" + raw

    [<Get ("/sync/%s/%s/%s")>]
    let sync ((source : string), (account : string), (project : string)) =

        let dataSource = 
            match source.ToLower() with
            | "azure" -> DataConfiguration.DataSource.AzureDevOps (account, project)
            | _       -> DataConfiguration.DataSource.Unsupported

        let token =
            match dataSource with
            DataConfiguration.DataSource.AzureDevOps(account,_)  ->
               (env (sprintf "AZURE_TOKEN_%s" <| account.ToUpper().Replace("-","_")) null)
            | source -> failwithf "Not supported. %A"source
        synchronize dataSource token

    [<Get "/test/%s/%s/%s">]
    let test (arg1, arg2, arg3) : int * string =

        200, arg1 + arg2 + arg3    
           

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
            Log.error null msg
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
                500,e.Message
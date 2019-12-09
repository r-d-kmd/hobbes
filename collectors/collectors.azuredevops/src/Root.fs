namespace Collector.AzureDevOps

open Hobbes.Db.Database
open Hobbes.Server.Db.Log
open Hobbes.Db
open Hobbes.Server.Routing
open Hobbes.Helpers 

[<RouteArea ("/", false)>]
module Root =

    [<Get "/ping">]
    let ping () =
        200, "ping"

    [<Get "/raw/%s/%s">]
    let raw (configurationName, theOther) : int * string =

        200, configurationName + theOther

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
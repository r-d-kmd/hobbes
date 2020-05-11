namespace Collector.AzureDevOps.Services

open Hobbes.Web.Routing
open Collector.AzureDevOps.Db
open Hobbes.Web.Database
open Hobbes.Web
open Hobbes.Helpers
open Hobbes.Shared.RawdataTypes
open Collector.AzureDevOps.Db.Rawdata

[<RouteArea ("/admin", false)>]
module Admin =

    let formatDBList name list =
        let stringList = 
            list
            |> Seq.map (sprintf "%A")

        let body = 
            System.String.Join(",", stringList)
            |> sprintf """{"%s" : [%s]}""" name 

        200, body  

    [<Get "/list/rawdata">]
    let listRawdata() =
        Rawdata.list() |> formatDBList "rawdata"

    [<Delete "/raw/%s">]
    let deleteRaw id =
        Rawdata.delete id       

    [<Get "/clear/rawdata">]
    let clearRawdata() =
        Rawdata.clear()  

    let createSyncDoc (config : Config.Root) (revision : string) =
        200, Rawdata.createSyncStateDocument revision config

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

    [<Get "/init">]
    let initDb () =
        let dbs = 
            [
                "rawdata"
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
                let documentDir = "db/documents"
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
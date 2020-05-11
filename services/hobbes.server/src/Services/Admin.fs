namespace Hobbes.Server.Services

open Hobbes.Web.Database
open Hobbes.Web.Log
open Hobbes.Web
open Hobbes.Server.Db
open Hobbes.Web.Routing
open Hobbes.Helpers.Environment

[<RouteArea "/admin">]
module Admin = 
    [<Literal>]
    let private SettingsPath = """./db/documents/settings.json"""
    type private Settings = FSharp.Data.JsonProvider<SettingsPath>

    [<Get ("/settings/%s/%s")>]
    let setting (area, setting) =
        200,couch.Get [
                    "_node" 
                    "_local"
                    "_config"
                    area 
                    setting
        ]

    let private configure (settings : Settings.Root []) =
        settings
        |> Array.fold(fun (status,msg) setting ->
            let value = 
                match setting.Value.Number with

                Some n -> n.ToString()
                | _ -> sprintf "%A" setting.Value.String.Value
            log value
            log (sprintf "%A" value)
            if status > 300 then
                status,msg
            else
                200,couch.Put ([
                                        "_node"
                                        "_local"
                                        "_config"
                                        setting.Area
                                        setting.Name
                           ],sprintf "%A"value)
        ) (200, "not started")

    [<Put ("/settings", true)>]
    let configureStr (settings : string) =
        let settings = 
            if settings.Trim().StartsWith("[") then
                settings
            else
               settings
               |>sprintf "[%s]"
        settings
        |> Settings.Parse
        |> configure

    [<Put ("/transformation",true)>]
    let storeTransformations doc =
        try
            match Http.post (None |> Http.Transformation |> Http.Configurations) id doc with
            Http.Success _ -> 200,sprintf """{"transformation":%s, "status" : "ok" }""" doc
            | Http.Error(s,m) -> s,m
        with e -> 
            Log.excf e "Trying to store %s" doc
            500,sprintf "internal server error"

    let formatDBList name list =
        let stringList = 
            list
            |> Seq.map (sprintf "%A")

        let body = 
            System.String.Join(",", stringList)
            |> sprintf """{"%s" : [%s]}""" name 

        200, body    

    [<Get ("/list/configurations")>]
    let listConfigurations() = 
        DataConfiguration.list()
        |> Seq.map(fun t -> t.Id) 
        |> formatDBList "configurations"
        
    [<Get ("/list/transformations")>]
    let listTransformations() = 
        Transformations.list()
        |> Seq.map(fun t -> t.Id) 
        |> formatDBList "transformations"             

    [<Get("/list/log")>]
    let listLog() = 
        list()
        |> Seq.map LogRecord.Parse
        |> Seq.sortByDescending(fun record -> record.Timestamp)
        |> Seq.map(fun logRecord ->
            let st = 
                match logRecord.Stacktrace with
                  None -> ""
                  | Some st -> sprintf "\n%s" st
                |> jsonify
            let message = logRecord.Message |> jsonify
            sprintf "%s - [%s] %s %s" (logRecord.Timestamp.ToString()) logRecord.Type message st
        ) |> formatDBList "logEntries"

    [<Put ("/configuration",true)>]
    let storeConfigurations doc = 
        try
            match Http.post (None |> Http.Configuration |> Http.Configurations) id doc with
            Http.Success _ -> 200,sprintf """{"configuration":%s, "status" : "ok" }""" doc
            | Http.Error(s,m) -> s,m
        with e -> 
            Log.excf e "Trying to store %s" doc
            500,sprintf "internal server error"
    
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
        
    let initDatabase () =
        async {
            let! _ = Hobbes.Web.Database.awaitDbServer()
            let systemDbs = 
                [
                    "_replicator"
                    "_global_changes"
                    "_users"
                    "log"
                ]
            let errorCode = 
                systemDbs
                |> List.map (fun n -> couch.TryPut(n, "") |> fst)
                |> List.tryFind (fun sc -> ((sc >= 200 && sc < 300) || (sc = 412)) |> not)
            match errorCode with
             Some errorCode ->
                let msg = "INIT: error in creating dbs"
                error null msg
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
                    log msg
                with e ->
                    errorf e.StackTrace "Error in init: %s" e.Message
        }

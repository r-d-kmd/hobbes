namespace Hobbes.Server.Services

open Hobbes.Server.Db.Database
open Hobbes.Server.Db.Log
open Hobbes.Server.Db
open Hobbes.Server.Routing

[<RouteArea "/admin">]
module Admin = 
    [<Literal>]
    let private SettingsPath = """./db/documents/settings.json"""
    type private Settings = FSharp.Data.JsonProvider<SettingsPath>

    [<Get ("/settings/%s/%s", """ "log" """, "Gets the value of a specific setting. This is meant for debugging and should be considered unstable")>]
    let setting (area, setting) =
        200,couch.Get [
                    "_node" 
                    "_local"
                    "_config"
                    area 
                    setting
        ]

    let configure (settings : Settings.Root []) =
        settings
        |> Array.fold(fun (status,msg) setting ->
            let value = 
                match setting.Value.Number with

                Some n -> n.ToString()
                | _ -> sprintf "%A" setting.Value.String.Value
            if status > 300 then
                status,msg
            else
                200,couch.Put ([
                                        "_node"
                                        "_local"
                                        "_config"
                                        setting.Area
                                        setting.Name
                           ],value)
        ) (200, "not started")

    [<Put ("/settings", "hobbes.server/src/db/documents/settings.json", """ "log" """, "Can be used for reconfiguring of the couch db. This is only meant for trouble shooting and should generally not be used")>]
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

    [<Put ("/transformation","{}", "", "Stores the transformation provided. The ID of the transformation can be referenced in a configuration and will then be applied to the data")>]
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

    [<Get ("/list/configurations","{}", "Lists all configurations. It's meant for debugging and should be considered unstable")>]
    let listConfigurations() = 
        DataConfiguration.list() |> formatDBList "configurations"

    [<Get ("/list/cache", "{}", "Meant for debuging and should be considered unstable. A list of all cache ids are returned")>]
    let listCache() = 
        Cache.list() |> formatDBList "cache"
        
    [<Get ("/list/transformations", "{}", "Meant for debuging and should be considered unstable. A list of all transformations are returned")>]
    let listTransformations() = 
        Transformations.list() |> formatDBList "transformations"
        
    [<Get ("/list/rawdata", "{}", "Meant for debuging and should be considered unstable. A list of all rawdata ids are returned")>]
    let listRawdata() = 
        Rawdata.list() |> formatDBList "rawdata"           

    [<Get("/list/log", "{}", "Returns the entire application log") >]
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

    [<Put ("/configuration","{}", "", "A configuration is a object specifying a data source and a series of transformation to be applied to the specified data. The configuration name/id is used as the argument when retrieving data with a data endpoint")>]
    let storeConfigurations doc = 
        try
            DataConfiguration.store doc |> ignore
            200,"ok"
        with _ -> 
            500,"internal server error"

    [<Delete ("/raw/%s", "Deletes a raw data entry with the provided id")>]
    let deleteRaw (id : string) = 
        Rawdata.delete id

    [<Delete ("/cache/%s", "Deletes the cache entry identified by the provided id")>]
    let deleteCache (id : string) = 
        Cache.delete id

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
        Settings.Load SettingsPath
        |> configure
        |> ignore
        
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
            error null msg
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
                        let db = Database(dbName, CouchDoc.Parse, ignoreLogging, "localhost:5984")
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

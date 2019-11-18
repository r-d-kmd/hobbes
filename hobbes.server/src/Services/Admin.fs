namespace Hobbes.Server.Services

open Hobbes.Server.Db.Database
open Hobbes.Server.Db.Log
open Hobbes.Server.Db
open Routing

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

    let configure (setting : Settings.Root) =
        let value = 
            match setting.Value.Number with

            Some n -> n.ToString()
            | _ -> sprintf "%A" setting.Value.String.Value
        200,couch.Put ([
                    "_node"
                    "_local"
                    "_config"
                    setting.Area
                    setting.Name
        ],value)

    [<Put ("/settings", "hobbes.server/src/db/documents/settings.json")>]
    let configureStr (settings) =
        (settings
         |>sprintf "[%s]"
         |> Settings.Parse).[0]
        |> configure

    [<Put ("/transformation","{}")>]
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

    [<Get ("/list/configurations")>]
    let listConfigurations() = 
        DataConfiguration.list() |> formatDBList "configurations"

    [<Get ("/list/cache")>]
    let listCache() = 
        Cache.list() |> formatDBList "cache"
        
    [<Get ("/list/transformations")>]
    let listTransformations() = 
        Transformations.list() |> formatDBList "transformations"
        
    [<Get ("/list/rawdata")>]
    let listRawdata() = 
        Rawdata.list() |> formatDBList "rawdata"           

    [<Get  "/list/log" >]
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

    [<Put ("/configuration","{}")>]
    let storeConfigurations doc = 
        try
            DataConfiguration.store doc |> ignore
            200,"ok"
        with _ -> 
            500,"internal server error"

    [<Delete "/raw/%s" >]
    let deleteRaw (id : string) = 
        Rawdata.delete id

    [<Delete "/cache/%s">]
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
        |> Array.iter(fun setting ->
             let value =
                setting.Value.Number
                |> Option.bind(string >> Some)
                |> Option.orElse(setting.Value.String)
                |> Option.get
             configure(setting) |> ignore
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

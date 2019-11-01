module Log

open FSharp.Data

type LogRecord = JsonProvider<"""{"_id" : "jlk",
                                  "timestamp" : "timestampId",
                                  "type" : "info|debug|error",
                                  "requestID" : "2342",
                                  "msg" : "This is a message",
                                  "stacktrace" : "This is a stacktrace"}""">

let private db = Database.Database ("log", LogRecord.Parse)

type LogType = 
    Info
    | Debug
    | Error
    | Unknown
    with override x.ToString() = 
            match x with
            Info      -> "info"
            | Debug   -> "debug"
            | Error   -> "error"
            | Unknown -> "unknown"
         static member Parse (s:string) =
                match s.ToLower() with
                "info"      -> Info
                | "debug"   -> Debug
                | "error"   -> Error
                | "unknown" -> Unknown
                | _         -> 
                            eprintfn "Unknown sync state: %s" s
                            Unknown
let private writeLogMessage timeStampID (logType : LogType) requestID msg stacktrace =
   let doc = sprintf """{"timestamp" : "%s",
                         "type" : "%A",
                         "requestID" : "%i",
                         "msg" : "%s",
                         "stacktrace" : "%s"}""" timeStampID logType requestID msg stacktrace
   db.Post doc |> ignore

let log msg =
    writeLogMessage null Info -1 msg null
    
let error msg stacktrace = 
    writeLogMessage null Error -1 msg stacktrace

let debug msg =
     writeLogMessage null Debug -1 msg null

let InsertOrUpdate doc = db.InsertOrUpdate doc

let tryGetHash id = db.TryGetHash id

let list() = 
   db.List()

let compactAndClean() = db.CompactAndClean()
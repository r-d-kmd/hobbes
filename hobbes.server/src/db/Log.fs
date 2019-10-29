module Log

open FSharp.Data

type LogRecord = JsonProvider<"""{"_id" : "jlk",
                                  "timestamp" : "timestampId",
                                  "type" : "info|debug|error",
                                  "requestID" : "2342",
                                  "msg" : "This is a message",
                                  "stacktrace" : "This is a stacktrace"}""">

let private db = Database.Database ("log", LogRecord.Parse)

let InsertOrUpdate doc = db.InsertOrUpdate doc

let tryGetRev id = db.TryGetRev id

let tryGetHash id = db.TryGetHash id

let list() = 
   db.List()

let compactAndClean() = db.CompactAndClean()
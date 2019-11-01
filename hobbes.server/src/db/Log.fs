namespace Hobbes.Server.Db

module Log =

   open FSharp.Data
   open FSharp.Core.Printf

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
   let private writeLogMessage timeStampID (logType : LogType) requestID stacktrace msg =
            let doc = sprintf """{"timestamp" : "%s",
                                  "type" : "%A",
                                  "requestId" : "%i",,
                                  "stacktrace" : "%s"
                                  "message" : "%s"}""" timeStampID logType requestID stacktrace msg
           
            doc |>
            db.Post |> ignore


   let log msg =
       writeLogMessage null Info -1 null msg

   let error stacktrace msg = 
       writeLogMessage  null Error -1 stacktrace msg

   let debug msg  =
       writeLogMessage null Debug -1 null msg

   let logf  format =
      ksprintf ( writeLogMessage null Info -1 null) format
      
   let errorf stacktrace format = 
      ksprintf (writeLogMessage null Error -1 stacktrace) format

   let debugf format =
       ksprintf ( writeLogMessage null Debug -1 null) format

   let InsertOrUpdate doc = db.InsertOrUpdate doc

   let tryGetHash id = db.TryGetHash id

   let list() = 
      db.List()

   let compactAndClean() = db.CompactAndClean()
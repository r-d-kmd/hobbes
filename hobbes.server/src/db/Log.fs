namespace Hobbes.Server.Db

module Log =
   let mutable logger = fun (_:string) -> ()
   open FSharp.Core.Printf

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
       async {
          try
             doc |> logger
          with e ->
              eprintfn "Failedto insert log doc %s. Message: %s StackTRace %s" doc e.Message e.StackTrace
       } |> Async.Start

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
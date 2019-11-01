namespace Hobbes.Server.Db

module Log =
    //need to make it possible to bootstrap the logger, since the db is also using the logger
    let mutable logger = eprintfn "%s"

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

    let private writeLogMessage (logType : LogType) stacktrace msg =
        let doc = sprintf """{"timestamp" : "%s",
                             "type" : "%A",
                             "stacktrace" : "%s",
                             "message" : "%s"}""" (System.DateTime.Now.ToString(System.Globalization.CultureInfo.InvariantCulture)) logType stacktrace msg
        async {
           try
              doc |> logger
           with e ->
               eprintfn "Failedto insert log doc %s. Message: %s StackTRace %s" doc e.Message e.StackTrace
        } |> Async.Start
 
    let log msg =
        writeLogMessage Info null msg
 
    let error stacktrace msg = 
        writeLogMessage  Error stacktrace msg
 
    let debug msg  =
        writeLogMessage Debug null msg
 
    let logf  format =
       ksprintf ( writeLogMessage Info null) format
       
    let errorf stacktrace format = 
       ksprintf (writeLogMessage Error stacktrace) format
 
    let debugf format =
        ksprintf ( writeLogMessage Debug null) format
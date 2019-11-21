namespace Hobbes.Server.Db
open FSharp.Data

module Log =
    open FSharp.Core.Printf
    type LogRecord = JsonProvider<"""[{"_id" : "jlk",
                                         "timestamp" : "timestampId",
                                         "type" : "info|debug|error",
                                         "message" : "This is a message",
                                         "stacktrace" : "This is a stacktrace"}, 
                                      {"_id" : "jlk",
                                         "timestamp" : "timestampId",
                                         "type" : "info|debug|error",
                                         "message" : "This is a message"}]""", SampleIsList=true>
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


    let mutable logLevel = 
#if DEBUG
        Debug
#else
        Error
#endif
    
    let mutable private _logger = eprintf "%A" 
    let mutable private _list : unit -> seq<string> = fun () -> Seq.empty
    
    let jsonify (str : string) =
        match str with
        null -> null
        | _ ->
            str.Replace("\"","'")
               .Replace("\\","\\\\")
               .Replace("\n","\\n")
               .Replace("\r","\\r")
               .Replace("\t","\\t")

    let private writeLogMessage (logType : LogType) stacktrace (msg : string) =
        let doc = sprintf """{"timestamp" : "%s",
                             "type" : "%A",
                             "stacktrace" : "%s",
                             "message" : "%s"}""" (System.DateTime.Now.ToString(System.Globalization.CultureInfo.InvariantCulture)) logType (stacktrace |> jsonify) (msg |> jsonify)
#if DEBUG
        //let's print log messages to console when running locally
        printfn "%s. \nStackTrace: %A" msg stacktrace
#else        
        async {
           try
              if logType >= logLevel then
                  doc |>_logger
           with e ->
               eprintfn "Failed to insert log doc %s. Message: %s StackTRace %s" doc e.Message e.StackTrace
        } |> Async.Start
#endif
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

    let timed requestName (ms : int64) = 
        let doc = sprintf """{"timestamp" : "%s",
                             "type" : "requestTiming",
                             "requestName" : "%s",
                             "executionTime" : %d}""" (System.DateTime.Now.ToString(System.Globalization.CultureInfo.InvariantCulture)) (requestName |> jsonify) ms
        async {
            try
                doc |> _logger
            with e ->
                eprintfn "Failedto insert timed event in log. %s. Message: %s StackTrace %s" doc e.Message e.StackTrace
        } |> Async.Start

    let list() = 
        _list()

    let loggerInstance = 
        { new Database.ILog with
            member __.Log msg   = log msg
            member __.Error stackTrace msg = error stackTrace msg
            member __.Debug msg = debug msg
            member __.Logf<'a> (format : Database.LogFormatter<'a>)  = logf format  
            member __.Errorf<'a> stackTrace (format : Database.LogFormatter<'a>) = errorf stackTrace format
            member __.Debugf<'a>  (format : Database.LogFormatter<'a>) = debugf format
        }

    let ignoreLogging =
        { new Database.ILog with
            member __.Log _   = ()
            member __.Error stackTrace msg = error stackTrace msg
            member __.Debug _ = ()
            member __.Logf<'a> (format : Database.LogFormatter<'a>)  = 
                ksprintf ignore format
            member __.Errorf<'a> stackTrace (format : Database.LogFormatter<'a>) = errorf stackTrace format
            member __.Debugf<'a>  (format : Database.LogFormatter<'a>)  = 
                ksprintf ignore format
        }
    do
        let db = Database.Database("log", LogRecord.Parse, ignoreLogging)
        _logger <- db.Post >> ignore
        _list <- (db.List >> Seq.map(fun l -> l.JsonValue.ToString(JsonSaveOptions.DisableFormatting)))
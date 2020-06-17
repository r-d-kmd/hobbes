
open Hobbes.Web
open Hobbes.Messaging
open Hobbes.Messaging.Broker
open Hobbes.Helpers

type CollectorList = FSharp.Data.JsonProvider<"""["azure devops","git"]""">
type SourceList = FSharp.Data.JsonProvider<"""[{
                      "name": "azure devops"
                    },{"name" : "git"}]""">
[<EntryPoint>]
let main _ =
    match Http.get (Http.Configurations Http.Collectors) CollectorList.Parse  with
    Http.Success collectors ->
        let sources =  
            collectors
            |> Array.collect(fun collector ->
                match Http.get (Http.Configurations (Http.Sources collector)) SourceList.Parse  with
                Http.Success sources ->
                    sources
                | Http.Error (sc,m) -> 
                    failwithf "Failed retrievining sources. %d - %s" sc m
            ) 
        Log.debugf "Syncronizing %d sources" (sources |> Seq.length)
        sources
        |> Array.iter(fun source ->
            let queueName = source.Name.ToLower().Replace(" ","")
            let message = 
                source.JsonValue.ToString()
                |> Sync
                |> Json.serialize
            Broker.Generic queueName message
        )
        0
    | Http.Error(sc,m) -> 
        failwithf "Failed retrievining collectors. %d - %s" sc m
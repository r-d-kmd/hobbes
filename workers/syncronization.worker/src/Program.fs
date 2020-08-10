
open Hobbes.Web
open Hobbes.Messaging
open Hobbes.Messaging.Broker
open Hobbes.Helpers

type CollectorList = FSharp.Data.JsonProvider<"""["azure devops","git"]""">
type SourceList = FSharp.Data.JsonProvider<"""[{
                "provider" : "azuredevops",
                "id" : "lkjlkj", 
                "project" : "gandalf",
                "dataset" : "commits",
                "server" : "https://analytics.dev.azure.com/kmddk/flowerpot"
            },{
                "provider" : "azuredevops",
                "id" : "lkjlkj", 
                "project" : "gandalf",
                "dataset" : "commits",
                "server" : "https://analytics.dev.azure.com/kmddk/flowerpot"
            }]""">
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
        let keys = 
            sources
        sources
        |> Array.iter(fun source ->
            let queueName = source.Provider.ToLower().Replace(" ","")
            source.JsonValue.ToString()
            |> Sync
            |> Message
            |> Json.serialize
            |> Broker.Generic queueName
        )
        0
    | Http.Error(sc,m) -> 
        failwithf "Failed retrievining collectors. %d - %s" sc m
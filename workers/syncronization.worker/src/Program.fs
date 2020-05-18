
open System.Text
open Hobbes.Helpers.Environment
open Hobbes.Web.RawdataTypes
open Hobbes.Web
open Hobbes.Workers.Shared.Queue

type CollectorList = FSharp.Data.JsonProvider<"""["azure devops","git"]""">
type SourceList = FSharp.Data.JsonProvider<"""[{
                      "name": "azure devops"
                    },{"name" : "git"}]""">
[<EntryPoint>]
let main _ =
    match Http.get (Http.Configurations Http.Collectors) CollectorList.Parse  with
    Http.Success collectors ->
        collectors
        |> Array.collect(fun collector ->
            match Http.get (Http.Configurations (Http.Sources collector)) SourceList.Parse  with
            Http.Success sources ->
                sources
            | Http.Error (sc,m) -> 
                failwithf "Failed retrievining sources. %d - %s" sc m
        ) |> Array.iter(fun source ->
            let queueName = source.Name
            let message = source.JsonValue.ToString()
            channel.QueueDeclare(queueName, true, false, false, null) |> ignore

            let body = System.ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(message))
            let properties = channel.CreateBasicProperties()
            properties.Persistent <- true

            channel.BasicPublish("",queueName, false,properties,body)
        )
        0
    | Http.Error(sc,m) -> 
        failwithf "Failed retrievining collectors. %d - %s" sc m
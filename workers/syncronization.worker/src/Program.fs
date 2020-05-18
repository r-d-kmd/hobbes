
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
            let queue = source.Name |> Queue.Generic
            let message = source.JsonValue.ToString()
            publish queue message
        )
        0
    | Http.Error(sc,m) -> 
        failwithf "Failed retrievining collectors. %d - %s" sc m
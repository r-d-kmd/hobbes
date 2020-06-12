namespace Hobbes.Gateway.Services

open Hobbes.Web.Log
open Hobbes.Web.Routing
open Hobbes.Helpers
open Hobbes.Web

[<RouteArea "/data">]
module Data = 
    let private cacheRevision confDoc = 
        sprintf "%s:%d" confDoc (System.DateTime.Now.Ticks) |> hash

    let rowToString =
        function
          Cache.Value.Int i -> sprintf "%i" i
        | Cache.Value.Float f -> sprintf "%f" f
        | Cache.Value.Date d -> sprintf "\"%A\"" d
        | Cache.Value.Text s -> s.Replace("\\", "\\\\")
                                |> sprintf "%A"
        | Cache.Value.Null -> "null"
        | Cache.Value.Boolean b -> sprintf "%b" b

    [<Get ("/json/%s")>]
    let json configuration =  
        debugf "Getting json for '%A'" configuration
        match Http.get (configuration |> Some |> Http.Configuration |> Http.Configurations) RawdataTypes.Config.Parse with
        Http.Success config -> 
            match Http.get (config.Source |> RawdataTypes.keyFromSource |> Http.CacheService.Read |> Http.UniformData) id with
            Http.Success json ->
                let record = Json.deserialize<Cache.CacheRecord> json
                let names = record.Data.ColumnNames
                let rows = record.Data.Values
                let formatted = Array.map (fun vals -> Array.map2 (fun n v -> sprintf "\"%s\" : %s" n (rowToString v)) names vals
                                                       |> String.concat ",") rows
                                |> String.concat "},{"
                                |> sprintf "[{%s}]"
                200, formatted
            | Http.Error(sc,m) -> sc,sprintf "Data for configuration %s not found. Message: %s" configuration m
        | Http.Error(sc,m) -> sc,sprintf "Configuration %s not found. Message: %s" configuration m
    
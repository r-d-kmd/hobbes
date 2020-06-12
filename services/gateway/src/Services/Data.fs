namespace Hobbes.Gateway.Services

open Hobbes.Web.Log
open Hobbes.Web.Routing
open Hobbes.Helpers
open Hobbes.Web

[<RouteArea "/data">]
module Data = 
    let private cacheRevision confDoc = 
        sprintf "%s:%d" confDoc (System.DateTime.Now.Ticks) |> hash

    [<Get ("/csv/%s")>]
    let csv configuration =  
        debugf "Getting csv for '%A'" configuration
        match Http.get (configuration |> Some |> Http.Configuration |> Http.Configurations) RawdataTypes.Config.Parse with
        Http.Success config -> 
            match Http.get (config.Source |> RawdataTypes.keyFromSource |> Http.CacheService.Read |> Http.UniformData) id with
            Http.Success json ->
                let data = Json.deserialize<Cache.DataResult> json
                200, """[{"id" : "ljkjkkkj",-1,"2019-12-31"},{"id" : "ljklkj",4,"2019-12-31"}]"""
            | Http.Error(sc,m) -> sc,sprintf "Data for configuration %s not found. Message: %s" configuration m
        | Http.Error(sc,m) -> sc,sprintf "Configuration %s not found. Message: %s" configuration m
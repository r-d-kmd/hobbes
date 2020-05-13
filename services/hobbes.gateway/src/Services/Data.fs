namespace Hobbes.Server.Services

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
        match Http.get (configuration |> Http.Calculate |> Http.Calculator) id with
        Http.Success csv ->
            200, csv
        | Http.Error(sc,m) -> sc,sprintf "Data for configuration %s not found. Message: %s" configuration m
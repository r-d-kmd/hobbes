namespace Hobbes.Server.Services

open Hobbes.Web.Log
open Hobbes.Server.Db
open Hobbes.Web.Routing
open Hobbes.Web.Http
open Hobbes.FSharp.DataStructures
open Hobbes.Helpers
open Hobbes.Server.Collectors

[<RouteArea "/data">]
module Data = 
    let cacheRevision confDoc = 
        sprintf "%s:%d" confDoc (System.DateTime.Now.Ticks) |> hash

    [<Get ("/csv/%s")>]
    let csv configuration =  
        debugf "Getting csv for '%A'" configuration
        match Hobbes.Web.Cache.Cache("calculator","data").Get configuration with
        Some data ->
            let csv = 
                data
                |> Hobbes.Web.Cache.readData
                |> DataMatrix.fromRows
                |> DataMatrix.toJson Csv
            debugf "CSV: %s" csv
            200, csv
        | None -> 404,sprintf "Data for configuration %s not found" configuration
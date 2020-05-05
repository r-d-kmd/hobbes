namespace Hobbes.Server.Services

open Hobbes.Web.Log
open Hobbes.Web.Routing
open Hobbes.Helpers

[<RouteArea "/data">]
module Data = 
    let cacheRevision confDoc = 
        sprintf "%s:%d" confDoc (System.DateTime.Now.Ticks) |> hash

    [<Get ("/csv/%s")>]
    let csv configuration =  
        debugf "Getting csv for '%A'" configuration
        match Hobbes.Web.Cache.Cache("calculator","calculate").Get configuration with
        Some data ->
            let csv = 
                let rows = 
                    data
                    |> Hobbes.Web.Cache.readData
                let columnNames = 
                    (":",rows
                         |> Seq.collect(fun (_,row) -> row |> Seq.map fst)
                         |> Seq.distinct
                         |> Seq.map(fun v -> v.Replace(":",";"))
                    ) |> System.String.Join
                let values = 
                    ("\n",rows
                          |> Seq.map(fun (_,row) ->
                              (":",row
                                   |> Seq.map(fun (_,v) ->
                                       let s = string v
                                       s.Replace(":",";")
                                   )
                              ) |> System.String.Join
                          )
                    ) |> System.String.Join 
                columnNames + "\n" + values
            debugf "CSV: %s" csv
            200, csv
        | None -> 404,sprintf "Data for configuration %s not found" configuration
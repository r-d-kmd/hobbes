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
        match Http.get (configuration |> Http.Calculate |> Http.Calculator) (Cache.CacheRecord.Parse >> Hobbes.Web.Cache.readData)  with
        Http.Success rows ->
            let csv = 
                let columnNames = 
                    (":",rows
                         |> Seq.collect(snd >> (Seq.map fst))
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
        | Http.Error(sc,m) -> sc,sprintf "Data for configuration %s not found. Message: %s" configuration m
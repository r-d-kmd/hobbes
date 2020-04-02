namespace Collector.Git.Services

open Hobbes.Server.Routing


[<RouteArea ("/data", false)>]
module Data =
     
    [<Get ("/sync/%s/%s")>]
    let sync (url, (pwd: string)) =
       Collector.Git.Reader.sync "RSL" pwd url
       200,"Synced"

    [<Get ("/data/%s/%s")>]
    let read (url,(dataset : string)) =
        let res = 
            match dataset.ToLower() with
            "commits" -> Collector.Git.Reader.commits url
            | "branches" -> Collector.Git.Reader.branches url
            | _ -> failwith "unknown dataset"
        200,res
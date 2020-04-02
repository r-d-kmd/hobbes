namespace Collector.GIt.Services

open Hobbes.Server.Routing
open Hobbes.Server.Db
open Hobbes.Web
open Hobbes.Helpers
open FSharp.Data


[<RouteArea ("/data", false)>]
module Data =

    let synchronize source token =
                                                                
             
    [<Get ("/sync/%s/%s")>]
    let sync (url, _) =
        Collector.Git.Reader.sync "RSL" url
    [<Get ("/data/%s/%s")>]
    let read (url,(dataset : string)) =
        match dataset.ToLower() with
        "commits" -> Collector.Git.Reader.commits url
        | "branches" -> Collector.Git.Reader.branches url
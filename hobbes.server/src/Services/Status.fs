namespace Hobbes.Server.Services

open Hobbes.Server.Db
open Hobbes.Web.Routing
open FSharp.Data
open Hobbes.Server.Collectors

[<RouteArea "/status">]
module Status =


    [<Get ("/sync/%s")>] 
    let getSyncState syncId =
        Collector.getSyncState "" syncId
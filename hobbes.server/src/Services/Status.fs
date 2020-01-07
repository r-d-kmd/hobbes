namespace Hobbes.Server.Services

open Hobbes.Server.Db
open Hobbes.Server.Routing
open FSharp.Data
open Hobbes.Server.Request

[<RouteArea "/status">]
module Status =


    [<Get ("/sync/%s")>] 
    let getSyncState syncId =
        sprintf "status/sync/%s" syncId
        |> get
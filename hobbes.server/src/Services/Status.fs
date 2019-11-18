namespace Hobbes.Server.Services

open Hobbes.Server.Db
open Routing

[<RouteArea "/status">]
module Status =
    [<Get "/sync/%s">] 
    let getSyncState syncId =
        200, (Rawdata.getState syncId).ToString()
      
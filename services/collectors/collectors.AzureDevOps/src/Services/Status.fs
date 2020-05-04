namespace Collector.AzureDevOps.Services

open Hobbes.Server.Routing
open Collector.AzureDevOps.Db


[<RouteArea ("/status", false)>]
module Status =    
    
    [<Get ("/sync/%s")>]
    let getState id =
        match Rawdata.getState id with
        None -> 404, "No sync doc found"
        | Some s -> 200,s
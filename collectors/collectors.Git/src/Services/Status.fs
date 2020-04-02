namespace Collector.AzureDevOps.Services

open Hobbes.Server.Routing
open Collector.AzureDevOps.Db


[<RouteArea ("/status", false)>]
module Status =    
    
    [<Get ("/sync/%s")>]
    let getState id =
        200, Rawdata.getState id
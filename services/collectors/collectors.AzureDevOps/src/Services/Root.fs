namespace Collector.AzureDevOps.Services

open Hobbes.Web.Routing


[<RouteArea ("/", false)>]
module Root =

    [<Get "/ping">]
    let ping () =
        200, "ping"
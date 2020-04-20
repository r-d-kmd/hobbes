open Saturn
open Giraffe
open Microsoft.AspNetCore.Http
open Hobbes.Server.Routing
open Collector.AzureDevOps.Services.Root
open Collector.AzureDevOps.Services.Data
open Collector.AzureDevOps.Services.Status
open Collector.AzureDevOps.Services.Admin
open Hobbes.Helpers.Environment

let private port = env "port" "8085"
                   |> int

let adminRouter = 
   router {
       fetch <@ listRawdata @>
       withArg <@ deleteRaw @>
       fetch <@ clearRawdata @>
       withArg <@ getRaw @>
       fetch <@ initDb @>
    }

let statusRouter = 
    router {
        withArg <@ getState @>
    }

let dataRouter = 
    router {
       //make a POST ops instead and let the arguments be in the body
       //to make it more flexible and "same" signature for all collectors
       withBody <@ sync @> 
       withBody <@ read @>
    }
    
let private appRouter = router {
    not_found_handler (setStatusCode 404 >=> text "The requested ressource does not exist")
    
    fetch <@ ping @>
    forward "/admin" adminRouter
    forward "/status" statusRouter
    forward "/data" dataRouter
} 

let private app = application {
    url (sprintf "http://0.0.0.0:%d/" port)
    use_router appRouter
    memory_cache
    use_gzip
}

let rec private init() =
    async {
        try
           FSharp.Data.Http.Request("http://collectordb-svc:5984") |> ignore //make sure db is up and running
           initDb() |> ignore
           printfn "DB initialized"
        with _ ->
           do! Async.Sleep 2000
           init()
    } |> Async.Start

init()
run app
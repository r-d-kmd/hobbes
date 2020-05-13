open Saturn
open Giraffe
open Microsoft.AspNetCore.Http
open Hobbes.Web.Routing
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
    }

let statusRouter = 
    router {
        withArg <@ getState @>
    }

let dataRouter = 
    router {
       withBody <@ read @>
       withBody <@ sync @>
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

Hobbes.Web.Database.initDatabases ["azure_devops_rawdata"]
run app
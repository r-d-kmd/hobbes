open Saturn
open Giraffe

open Hobbes.Web.Routing
open Collector.Git.Services.Root
open Collector.Git.Services.Data
open Hobbes.Helpers.Environment

let private port = env "port" "8085"
                   |> int

let dataRouter = 
    router {
       withBody <@ sync @>
       withBody <@ read @>
    }
    
let private appRouter = router {
    not_found_handler (setStatusCode 404 >=> text "Api 404")
    
    fetch <@ ping @>
    forward "/data" dataRouter
} 

let private app = application {
    url (sprintf "http://0.0.0.0:%d/" port)
    use_router appRouter
    memory_cache
    use_gzip
}


run app
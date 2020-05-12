open Saturn
open Giraffe
open Hobbes.UniformData.Services.Data
open Hobbes.Web.Routing
open Hobbes.Helpers.Environment

let private port = env "PORT" "8085"
                   |> int

let private appRouter = router {
    not_found_handler (setStatusCode 404 >=> text "The requested ressource does not exist")
    
    fetch <@ ping @>
    withArg <@ read @>
    withBody <@ update @>
} 

let private app = application {
    url (sprintf "http://0.0.0.0:%d/" port)
    use_router appRouter
    memory_cache
    use_gzip
}


Hobbes.Web.Database.initDatabases ["uniformcache"]
run app
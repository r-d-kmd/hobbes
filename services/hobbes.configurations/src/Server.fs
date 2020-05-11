open Saturn
open Giraffe
open Hobbes.Calculator.Services.Data
open Hobbes.Web.Routing
open Hobbes.Helpers.Environment

let private port = env "PORT" "8085"
                   |> int

let private appRouter = router {
    not_found_handler (setStatusCode 404 >=> text "The requested ressource does not exist")
    
    fetch <@ ping @>
    withArg <@ configuration @>
    withArg <@ transformation @>
    withBody <@ storeConfiguration @>
    withBody <@ storeTransformation @>
} 

let private app = application {
    url (sprintf "http://0.0.0.0:%d/" port)
    use_router appRouter
    memory_cache
    use_gzip
}



[
   "configurations"
   "transformations"
   "sources"
] |> Hobbes.Web.Database.initDatabases
run app
open Saturn
open Giraffe
open Hobbes.UniformData.Services
open Hobbes.Web.Routing
open Hobbes.Helpers.Environment

let private port = env "PORT" "8085"
                   |> int

let uniformDataRouter = 
    router {
        fetch <@ UniformData.ping @>
        withArg <@ UniformData.read @>
        withBody <@ UniformData.update @>
    }

let dataSetRouter = 
    router {
        fetch <@ DataSet.ping @>
        withArg <@ DataSet.read @>
        withBody <@ DataSet.update @>
    }

let private appRouter = router {
    not_found_handler (setStatusCode 404 >=> text "The requested ressource does not exist")
    
    forward "/uniform" uniformDataRouter
    forward "/dataset" dataSetRouter
} 

let private app = application {
    url (sprintf "http://0.0.0.0:%d/" port)
    use_router appRouter
    memory_cache
    use_gzip
}


Hobbes.Web.Database.initDatabases ["uniformcache"]
run app
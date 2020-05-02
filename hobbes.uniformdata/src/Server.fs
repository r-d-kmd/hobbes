open Saturn
open Giraffe
open Hobbes.UniformData.Services.Data
open Hobbes.Server.Routing
open Hobbes.Helpers.Environment

let private port = env "port" "8085"
                   |> int
let private databaseServerUrl = env "DB_SERVER_URL" null

let dataRouter = 
    router {
       withBody <@ read @>
    }
    
let private appRouter = router {
    not_found_handler (setStatusCode 404 >=> text "The requested ressource does not exist")
    
    fetch <@ ping @>
    forward "/data" dataRouter
} 

let private app = application {
    url (sprintf "http://0.0.0.0:%d/" port)
    use_router appRouter
    memory_cache
    use_gzip
}

let rec private init() =
    if databaseServerUrl |> isNull then 
        failwith "Database server URL not configured"
    async {

        try
           FSharp.Data.Http.Request(databaseServerUrl) |> ignore //make sure db is up and running
           FSharp.Data.Http.Request(databaseServerUrl + "/uniform",
                                    httpMethod = "PUT") 
                                   |> ignore
           printfn "DB initialized"
        with _ ->
           do! Async.Sleep 2000
           init()
    } |> Async.Start

init()
run app
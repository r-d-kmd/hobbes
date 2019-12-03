open Saturn
open Giraffe
open Microsoft.AspNetCore.Http
open AzureDevopsCollector

let env name = 
    System.Environment.GetEnvironmentVariable name

let private port = 
    match env "port" with
    null -> 8085
    | p -> int p
    
let private appRouter = router {
    not_found_handler (setStatusCode 404 >=> text "Api 404")
    
    get "/ping" ((ignore >> Implementation.ping) |> Hobbes.Server.Routing.noArgs "ping" )
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
           FSharp.Data.Http.Request("http://collectordb:5984") |> ignore //make sure db is up and running
           Implementation.initDb() |> ignore
           printfn "DB initialized"
        with _ ->
           do! Async.Sleep 2000
           init()
    } |> Async.Start

init()
run app
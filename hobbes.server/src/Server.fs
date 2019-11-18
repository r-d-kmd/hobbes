open Saturn
open Giraffe
open Hobbes.Server.Db.Database
open Implementation

let private port = 
    match env "port" with
    null -> 8085
    | p -> int p

open Routing 

let private appRouter = router {
    not_found_handler (setStatusCode 404 >=> text "Api 404")
    
    fetch <@ ping @> 
    withArg <@ key @>
    collect "/admin"
    collect "/status"
    collect "/data"
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
           Hobbes.Server.Services.Admin.initDb() |> ignore
           printfn "DB initialized"
        with _ ->
           do! Async.Sleep 2000
           init()
    } |> Async.Start

init()
run app
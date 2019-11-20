open Saturn
open Giraffe
open Hobbes.Server.Db.Database

let private port = 
    env "port" "8085" |> int

open Hobbes.Server.Routing 

let private rootRouter = router {
    collect "/"
} 

let private adminRouter = router {
    collect "/admin"
} 

let private statusRouter = router {
    collect "/status"
} 

let private dataRouter = router {
    collect "/data"
} 

let private appRouter = router {
    not_found_handler (setStatusCode 404 >=> text "Api 404")
    
    forward "/" rootRouter
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
           Hobbes.Server.Services.Admin.initDb() |> ignore
           printfn "DB initialized"
        with _ ->
           do! Async.Sleep 2000
           init()
    } |> Async.Start

init()
run app
open Saturn
open Giraffe
open Microsoft.AspNetCore.Http
open Hobbes.Server.Db.Database
open Hobbes.Server.Db
open Implementation

let private port = 
    match env "port" with
    null -> 8085
    | p -> int p

open Routing  

let adminRouter = 
   router {
        withBody <@ storeTransformations @>
        fetch    <@ listConfigurations@>
        fetch    <@ listTransformations @>
        fetch    <@ listCache @>
        fetch    <@ listRawdata @>
        fetch    <@ listLog @> 

        withArg  <@ deleteRaw @>
        withArg  <@ deleteCache @>
        withArgs <@ setting @>
        withBody <@ configureStr @>
        withBody <@storeConfigurations@>
    }

let statusRouter = 
    router {
        pipe_through verifiedPipe
        withArg <@ getSyncState @>
    }

let dataRouter = 
    router {
        pipe_through verifiedPipe
        withArg <@ csv @> 
        withArg <@sync@>
        withArg <@ getRaw @>
    }

let private appRouter = router {
    not_found_handler (setStatusCode 404 >=> text "Api 404")
    
    fetch <@ ping @> 
    withArg <@ key @>
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
           initDb() |> ignore
           printfn "DB initialized"
        with _ ->
           do! Async.Sleep 2000
           init()
    } |> Async.Start

init()
run app
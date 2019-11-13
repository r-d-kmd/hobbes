open Saturn
open Giraffe
open Microsoft.AspNetCore.Http
open Collector

let env name = 
    System.Environment.GetEnvironmentVariable name

let private port = 
    match env "port" with
    null -> 8085
    | p -> int p

let private sync (ctx : HttpContext) configurationName =
        try
            match ctx.TryGetRequestHeader "PAT" with
            Some azurePAT -> Implementation.sync azurePAT configurationName
            | None ->  403, "Unauthorized"
        with e -> 
           // Log.errorf e.StackTrace "Couldn't sync %s. Reason: %s" configurationName e.Message
            500, e.Message
    
let statusRouter = 
    router {
        getf "/sync/%s" (Implementation.getSyncState |> Routing.skipContext |> Routing.withArgs  "status/sync")
    }

let dataRouter = 
    router {
        getf "/csv/%s" (Implementation.csv |> Routing.skipContext |> Routing.withArgs "csv" ) 
        getf "/sync/%s" ( sync |> Routing.withArgs "sync" )
        getf "/raw/%s" (Implementation.getRaw |> Routing.skipContext |> Routing.withArgs "raw" )
    }

let private appRouter = router {
    not_found_handler (setStatusCode 404 >=> text "Api 404")
    
    get "/ping" ((ignore >> Implementation.ping) |> Routing.noArgs "ping" )
    get "/init" ((ignore >> Implementation.initDb) |> Routing.noArgs "init") 
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
           Implementation.initDb() |> ignore
           printfn "DB initialized"
        with _ ->
           do! Async.Sleep 2000
           init()
    } |> Async.Start

init()
run app
open Saturn
open Giraffe
open Microsoft.AspNetCore.Http
open Hobbes.Server.Db.Database
open Hobbes.Server.Db

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
            Log.errorf e.StackTrace "Couldn't sync %s. Reason: %s" configurationName e.Message
            500, e.Message

//TODO We should split this in multiple sub routers
//See Saturn best practices for advice
//TODO Use pipelines to do the verification
let private apiRouter = router {
    not_found_handler (setStatusCode 404 >=> text "Api 404")
    
    get "/ping" ((ignore >> Implementation.ping) |> Routing.unverified "ping" ) 
    getf "/key/%s" (Implementation.key |> Routing.skipContext |> Routing.unverifiedWithArgs "key")
    
    get "/init" ((ignore >> Implementation.initDb) |> Routing.unverified "initDb") 
    getf "/csv/%s" (Implementation.csv |> Routing.skipContext |> Routing.verifiedWithArgs "csv" ) 
    getf "/sync/%s" ( sync |> Routing.verifiedWithArgs "sync" )

    put "/configurations" (Implementation.storeConfigurations |> Routing.withBodyNoArgs "configurations")
    put "/transformations" (Implementation.storeTransformations |> Routing.withBodyNoArgs "transformations")
    get "/list/configurations" (Implementation.listConfigurations  |> Routing.verified "list/configurations")
    get "/list/transformations" (Implementation.listTransformations |> Routing.verified "list/transformations" )
    get "/list/cache" (Implementation.listCache |> Routing.verified "list/cache")
    get "/list/rawdata" (Implementation.listRawdata |> Routing.verified "list/rawdata")
    get "/list/log" (Implementation.listLog |> Routing.verified "list/log")
    getf "/status/sync/%s" (Implementation.getSyncState |> Routing.skipContext |> Routing.verifiedWithArgs  "status/sync")
    getf "/admin/settings/%s/%s" ( Implementation.setting |> Routing.skipContext |> Routing.verifiedWithArgs "admin/settings")
    putf "/admin/settings/%s/%s/%s" (Implementation.configure |> Routing.skipContext  |> Routing.verifiedWithArgs "admin/settings")
} 

let private appRouter = router {
    forward "" apiRouter
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
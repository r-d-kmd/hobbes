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
let adminRouter = 
    router {
        pipe_through Routing.verifiedPipe
        put "/configuration" (Implementation.storeConfigurations |> Routing.withBodyNoArgs "configuration")
        put "/transformation" (Implementation.storeTransformations |> Routing.withBodyNoArgs "transformation")
        get "/list/configurations" (Implementation.listConfigurations  |> Routing.noArgs "list/configurations")
        get "/list/transformations" (Implementation.listTransformations |> Routing.noArgs "list/transformations" )
        get "/list/cache" (Implementation.listCache |> Routing.noArgs "list/cache")
        get "/list/rawdata" (Implementation.listRawdata |> Routing.noArgs "list/rawdata")
        get "/list/log" (Implementation.listLog |> Routing.noArgs "list/log")
        deletef "/raw/%s" (Rawdata.delete |> Routing.skipContext |> Routing.withArgs "raw" )
        deletef "/cache/%s" (Cache.delete |> Routing.skipContext |> Routing.withArgs "cache" )
        getf "/settings/%s/%s" ( Implementation.setting |> Routing.skipContext |> Routing.withArgs "admin/settings")
        putf "/settings/%s/%s/%s" (Implementation.configure |> Routing.skipContext  |> Routing.withArgs "admin/settings")
    }
    
let statusRouter = 
    router {
        pipe_through Routing.verifiedPipe
        getf "/sync/%s" (Implementation.getSyncState |> Routing.skipContext |> Routing.withArgs  "status/sync")
    }

let dataRouter = 
    router {
        pipe_through Routing.verifiedPipe
        getf "/csv/%s" (Implementation.csv |> Routing.skipContext |> Routing.withArgs "csv" ) 
        getf "/sync/%s" ( sync |> Routing.withArgs "sync" )
        getf "/raw/%s" (Rawdata.get |> Routing.skipContext |> Routing.withArgs "raw" )
    }

//TODO We should split this in multiple sub routers
//See Saturn best practices for advice
//TODO Use pipelines to do the verification
let private appRouter = router {
    not_found_handler (setStatusCode 404 >=> text "Api 404")
    
    get "/ping" ((ignore >> Implementation.ping) |> Routing.noArgs "ping" ) 
    getf "/key/%s" (Implementation.key |> Routing.skipContext |> Routing.withArgs "key")
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
           Implementation.initDb() |> ignore
           printfn "DB initialized"
        with _ ->
           do! Async.Sleep 2000
           init()
    } |> Async.Start

init()
run app
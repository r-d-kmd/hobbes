open Saturn
open Giraffe
open Microsoft.AspNetCore.Http
open Hobbes.Server.Db.Database
open Hobbes.Server.Db
open Hobbes.Server.Routing
open Hobbes.Server.Services.Admin
open Hobbes.Server.Services.Data
open Hobbes.Server.Services.Root
open Hobbes.Server.Services.Status

let private port = 
    env "port" "8085" |> int
    
let adminRouter = 
   router {
        pipe_through verifiedPipe

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
        withArg <@ syncronize @>
        withArg <@ getRaw @>
    }

let private appRouter = router {
    not_found_handler (setStatusCode 404 >=> text "Api 404")
    
    fetch <@ ping @> 
    withArg <@ key @>
    fetch <@ initDb @>
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
           FSharp.Data.Http.Request("http://db:5984") |> ignore //make sure db is up and running
           initDb() |> ignore
           printfn "DB initialized"
        with _ ->
           do! Async.Sleep 2000
           init()
    } |> Async.Start
let appInfo = Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application        
printf """{"appVersion": "%s", "runtimeFramework" : "%s", "appName" : "%s"}""" appInfo.ApplicationVersion appInfo.RuntimeFramework.FullName appInfo.ApplicationName
init()
run app
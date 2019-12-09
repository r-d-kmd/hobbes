open Saturn
open Giraffe
open Hobbes.Server.Routing
open Hobbes.Server.Services.Admin
open Hobbes.Server.Services.Data
open Hobbes.Server.Services.Root
open Hobbes.Server.Services.Status
open Hobbes.Helpers

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
        fetch    <@ clearCache @> 
        fetch    <@ clearRawdata @> 

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
    withBody <@ key @>
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
           FSharp.Data.Http.Request(env "DB_SERVER_URL" "http://db-svc:5984") |> ignore //make sure db is up and running
           initDb() |> ignore
           printfn "DB initialized"
        with _ ->
           do! Async.Sleep 2000
           init()
    } |> Async.Start

let asm = System.Reflection.Assembly.GetExecutingAssembly() 
let asmName = asm.GetName()

let version = asmName.Version.ToString()      
printfn """{"appVersion": "%s", "name" : "%s"}""" version asmName.Name
init()
run app
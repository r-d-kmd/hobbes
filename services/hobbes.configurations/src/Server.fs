open Saturn
open Giraffe
open Hobbes.Calculator.Services.Data
open Hobbes.Server.Routing
open Hobbes.Helpers.Environment

let private port = env "PORT" "8085"
                   |> int
let private databaseServerUrl = env "DB_SERVER_URL" null

let private appRouter = router {
    not_found_handler (setStatusCode 404 >=> text "The requested ressource does not exist")
    
    fetch <@ ping @>
    withArg <@ configuration @>
    withArg <@ transformation @>
} 

let private app = application {
    url (sprintf "http://0.0.0.0:%d/" port)
    use_router appRouter
    memory_cache
    use_gzip
}

let rec private init databaseToBeInitialized =
    if databaseServerUrl |> isNull then 
        failwith "Database server URL not configured"
    async {
        let httpMethod = "PUT"
        printfn "Testing of db server is reachable on %s" databaseServerUrl
        FSharp.Data.Http.Request(databaseServerUrl) |> ignore //make sure db is up and running
        let dbUser = 
            match env "COUCHDB_USER" null with
            null -> failwith "DB user not configured"
            | user -> user

        let dbPwd = 
            match env "COUCHDB_PASSWORD" null with
            null -> failwith "DB password not configured"
            | pwd -> pwd
        
        let failed = 
           databaseToBeInitialized
           |> List.filter(fun name ->
            
            let url = databaseServerUrl + "/" + name
            printfn "Creating database. %s on %s" httpMethod url
            let resp = 
               FSharp.Data.Http.Request(url,
                                        httpMethod = httpMethod,
                                        silentHttpErrors = true,
                                        headers = [FSharp.Data.HttpRequestHeaders.BasicAuth dbUser dbPwd]
                                       ) 
            match resp.StatusCode with
            200 ->
               printfn "Database created"
               false
            | 412 -> 
               printfn "Database already existed"
               false
            | 401 -> 
                failwith "DB user not configured correctly" 
            | _ ->
               eprintfn "Database creation failed with %d - %s. Will try again" resp.StatusCode (resp.Body |> Hobbes.Web.Http.readBody)
               true
           )
        if failed |> List.isEmpty |> not then
            do! Async.Sleep 2000
            init failed
        else
            printfn "DB initialized"
    } |> Async.Start

[
   "configurations"
   "transformations"
   "sources"
] |> init
run app
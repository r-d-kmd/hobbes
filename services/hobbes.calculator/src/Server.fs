open Saturn
open Giraffe
open Hobbes.Calculator.Services.Data
open Hobbes.Server.Routing
open Hobbes.Helpers.Environment

let private port = env "PORT" "8085"
                   |> int
let private databaseServerUrl = env "DB_SERVER_URL" null

let dataRouter = 
    router {
       withBody <@ calculate @>
    }
    
let private appRouter = router {
    not_found_handler (setStatusCode 404 >=> text "The requested ressource does not exist")
    
    fetch <@ ping @>
    forward "/data" dataRouter
} 

let private app = application {
    url (sprintf "http://0.0.0.0:%d/" port)
    use_router appRouter
    memory_cache
    use_gzip
}

let rec private init() =
    if databaseServerUrl |> isNull then 
        failwith "Database server URL not configured"
    let dbUser = 
            match env "COUCHDB_USER" null with
            null -> failwith "DB user not configured"
            | user -> user

    let dbPwd = 
        match env "COUCHDB_PASSWORD" null with
        null -> failwith "DB password not configured"
        | pwd -> pwd
    async {
            printfn "Testing of db server is reachable on %s" databaseServerUrl
            FSharp.Data.Http.Request(databaseServerUrl) |> ignore //make sure db is up and running
            let httpMethod = "PUT"
            let url = databaseServerUrl + "/transformationcache"
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
            | 412 -> 
               printfn "Database already existed"
            | 401 -> 
               failwithf "DB user not configured correctly"
            | _ ->
               eprintfn "Database creation failed with %d - %s. Will try again" resp.StatusCode (resp.Body |> Hobbes.Web.Http.readBody)
               do! Async.Sleep 2000
               init()
    } |> Async.Start

init()
run app
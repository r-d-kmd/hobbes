open Saturn
open Giraffe.Core
open Giraffe.ResponseWriters
open Microsoft.AspNetCore.Http
open Hobbes.FSharp.DataStructures
open Database
open Hobbes.Server.Db
open System.Security.Cryptography
open System.IO
open Hobbes.Server.Security
let private port = 
    match env "port" with
    null -> 8085
    | p -> int p

let private enumeratorMap f (e : System.Collections.IEnumerator) = //Shouldn't be here
    let rec aux acc =
        match e.MoveNext() with
        true -> aux (f e.Current :: acc)
        | false -> acc
    aux []
      

let private data configurationName =
    
    fun func (ctx : HttpContext) ->
        let statusCode, body =  
            match ctx.TryGetRequestHeader "Authorization" with
            None ->    
                eprintfn "Tried to gain access without a key"
                403, "Unauthorized"
            | Some authToken ->
                    if authToken |> verifyAuthToken then
                        let data = 
                            match cache.TryGet configurationName with
                            None -> 
                                let configuration = DataConfiguration.get configurationName
                                let datasetKey =
                                    sprintf "%s:%s" (configuration.Source.SourceName) (configuration.Source.ProjectName)
                                let rawData =
                                    match Rawdata.list datasetKey with
                                    s when s |> Seq.isEmpty -> 
                                        DataCollector.get configuration.Source |> ignore
                                        Rawdata.list datasetKey
                                    | data -> 
                                        data

                                let transformations = 
                                        Transformations.load configuration.Transformations
                                        |> Array.collect(fun t -> t.Lines)
                                let func = Hobbes.FSharp.Compile.expressions transformations                                                                   
                                (rawData
                                 |> Seq.map(fun (columnName,values) -> 
                                    columnName, values.ToSeq()
                                                 |> Seq.map(fun (i,v) -> Hobbes.Parsing.AST.KeyType.Create i, v)
                                 ) |> DataMatrix.fromTable
                                 |> func).ToJson()
                                |> Cache.store configurationName 
                                
                            | Some cacheRecord -> cacheRecord.Data
                        (200, data) |> Some
                    else
                       None
                    |> Option.orElseWith(fun () ->
                        eprintfn "Tried to gain access with an invalid key. Token (%s)" authToken
                        (403, "Unauthorized") |> Some
                    ) |> Option.get
        (setStatusCode statusCode
         >=> setBodyFromString body) func ctx

let private helloWorld =
    setStatusCode 200
    >=> setBodyFromString "Hello Lucas"

   
let private key token =
    let user = 
        token
        |> tryParseUser
        |> Option.bind(fun (user,token) -> 
              let userId = sprintf "org.couchdb.user%%3A%s" user
              match users.TryGet userId with
              None ->
                printfn "Didn't find user. %s" userId
                let userRecord = 
                    sprintf """{
                      "name": "%s",
                      "type": "user",
                      "roles": [],
                      "password": "%s"
                    }""" user token
                userRecord
                |> users.Put userId
                users.Get userId
                |> Some
              | s -> s
        )

    match user with
    None ->
        eprintfn "No user token. Tried with %s" token 
        setStatusCode 403 >=> setBodyFromString "Unauthorized"
    | Some (user) ->
        printfn "Creating api key for %s" user.Name
        let key = createToken user
        setStatusCode 200 >=> setBodyFromString key

let private apiRouter = router {
    not_found_handler (setStatusCode 404 >=> text "Api 404")
    
    getf "/data/%s" data
    get "/helloServer" helloWorld
    getf "/key/%s" key
    get "/ping" (setStatusCode 200 >=> setBodyFromString "pong")
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

run app
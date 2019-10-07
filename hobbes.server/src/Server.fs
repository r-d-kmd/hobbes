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
      
let stopwatch = System.Diagnostics.Stopwatch()
stopwatch.Start()

let private verifiedAndTimed serviceName f =
    fun func (ctx : HttpContext) ->
        let start = stopwatch.ElapsedMilliseconds
        let statusCode, body =  
            match ctx.TryGetRequestHeader "Authorization" with
            None ->    
                eprintfn "Tried to gain access without a key"
                403, "Unauthorized"
            | Some authToken ->
                if authToken |> verifyAuthToken then
                    f()
                else 
                    403, "Unauthorized"
        let responseTime = stopwatch.ElapsedMilliseconds - start
        printfn "[PERF] %s loaded in: %d ms" serviceName responseTime
        (setStatusCode statusCode
         >=> setBodyFromString body) func ctx

let private data configurationName =
    let executer() =
        let cacheKey = configurationName
        let data = 
            match cacheKey
                  |> Cache.tryRetrieve with
            None ->
                printfn "Cache miss %s" cacheKey
                let configuration = DataConfiguration.get configurationName
                let datasetKey =
                    [configuration.Source.SourceName;configuration.Source.ProjectName]
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
                 |> func).ToJson(Column)
                |> Cache.store cacheKey
            | Some data ->
                printfn "Cache hit %s" cacheKey
                data
        (200, data.ToString())
    verifiedAndTimed (sprintf "data - configuration: %s" configurationName) executer
        

let private helloWorld =
    setStatusCode 200
    >=> setBodyFromString "Hello Lucas"

let private sync configurationName =
    let executer() =
        try
            let configuration = DataConfiguration.get configurationName
            match configuration.Source with
            DataConfiguration.AzureDevOps projectName ->
                let rec _sync (url : string) = 
                    let record = Database.AzureDevOpsAnalyticsRecord.Load url
                    Rawdata.store configuration.Source.SourceName configuration.Source.ProjectName url (record.ToString())
                    if System.String.IsNullOrWhiteSpace(record.OdataNextLink) |> not then
                        _sync record.OdataNextLink
                sprintf "https://analytics.dev.azure.com/kmddk/%s/_odata/v2.0/WorkItemRevisions?$expand=Iteration&$select=WorkITemId%%2CWorkItemType%%2CState%%2CStateCategory%%2CIteration&$filter=Iteration%%2FStartDate%%20gt%%202019-01-01Z" projectName
                |> _sync
                200,"Synced" 
            | _ -> 
                404,"No reader found" 
        with e -> 
            eprintfn "Couldn't sync %s. Reason: %s" configurationName e.Message
            500, e.Message
            
    verifiedAndTimed (sprintf "sync - configuration: %s" configurationName) executer


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
    getf "/sync/%s" sync
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
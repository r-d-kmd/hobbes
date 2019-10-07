open Saturn
open Giraffe.Core
open Giraffe.ResponseWriters
open Microsoft.AspNetCore.Http
open Hobbes.FSharp.DataStructures
open Database
open Hobbes.Server.Db
open FSharp.Data
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
      
let private verified f =
    fun func (ctx : HttpContext) ->
        let statusCode, body =  
            match ctx.TryGetRequestHeader "Authorization" with
            None ->    
                eprintfn "Tried to gain access without a key"
                403, "Unauthorized"
            | Some authToken ->
                if authToken |> verifyAuthToken then
                    f ctx
                else 
                    403, "Unauthorized"
        (setStatusCode statusCode
         >=> setBodyFromString body) func ctx

let private data configurationName =
    let executer _ =
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
    verified executer
       
let request user pwd httpMethod body url  =
    let headers =
        [
            HttpRequestHeaders.BasicAuth user pwd
            HttpRequestHeaders.ContentType HttpContentTypes.Json
        ]
    match body with
    None -> 
        Http.Request(url,
            httpMethod = httpMethod, 
            silentHttpErrors = true,
            headers = headers
        )
    | Some body ->
        Http.Request(url,
            httpMethod = httpMethod, 
            silentHttpErrors = true, 
            body = TextRequest body,
            headers = headers
        )

let private sync configurationName =
    let executer (ctx : HttpContext) =
        try
            match ctx.TryGetRequestHeader "PAT" with
            Some pat ->
                let configuration = DataConfiguration.get configurationName
                match configuration.Source with
                DataConfiguration.AzureDevOps projectName ->
                    let rec _sync (url : string) = 
                        let resp = 
                            url
                            |> request pat pat "GET" None
                        if resp.StatusCode = 200 then
                            let record = 
                                match resp.Body with
                                Text body ->
                                    body
                                    |> AzureDevOpsAnalyticsRecord.Parse
                                    |> Some
                                | _ -> 
                                    None
                            match record with
                            Some record ->
                                let responseText = Rawdata.store configuration.Source.SourceName configuration.Source.ProjectName url (record.ToString())
                                if System.String.IsNullOrWhiteSpace(record.OdataNextLink) |> not then
                                    printfn "Countinuing sync"
                                    printfn "%s" record.OdataNextLink
                                    _sync record.OdataNextLink
                                else 
                                    200, responseText
                            | None -> 500, "Couldn't parse record"
                        else 
                            resp.StatusCode, (match resp.Body with Text t -> t | _ -> "")
                    let statusCode,message = 
                        let selectedFields = 
                           (",", [
                             "ChangedDate"
                             "WorkITemId"
                             "WorkItemType"
                             "State"
                             "StateCategory"
                             "Iteration"
                             "LeadTimeDays"
                             "CycleTimeDays"
                           ]) |> System.String.Join
                        sprintf "https://analytics.dev.azure.com/kmddk/%s/_odata/v2.0/WorkItemRevisions?$expand=Iteration&$select=%s&$filter=IsLastRevisionOfDay%%20eq%%20true%%20and%%20Iteration%%2FStartDate%%20gt%%202019-01-01Z" projectName selectedFields
                        |> _sync
                    statusCode,message
                | _ -> 
                    404,"No reader found" 
            | None -> 403,"Unauthorized"
        with e -> 
            eprintfn "Couldn't sync %s. Reason: %s" configurationName e.Message
            500, e.Message
            
    verified executer


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
                |> ignore
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
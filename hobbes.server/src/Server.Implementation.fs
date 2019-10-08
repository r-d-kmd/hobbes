module Implementation

open Hobbes.FSharp.DataStructures
open Database
open Hobbes.Server.Db
open FSharp.Data
open Hobbes.Server.Security

let data configurationName =
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
    200,data.ToString()
       
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

let sync pat configurationName =
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
                    let data = record.JsonValue.ToString JsonSaveOptions.DisableFormatting
                    let responseText = Rawdata.store configuration.Source.SourceName configuration.Source.ProjectName url data
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
    
let key token =
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
        403,"Unauthorized"
    | Some (user) ->
        printfn "Creating api key for %s" user.Name
        let key = createToken user
        200,key
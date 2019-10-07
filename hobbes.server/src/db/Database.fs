module Database

open FSharp.Data

let env name = 
    System.Environment.GetEnvironmentVariable name

let private user = env "COUCHDB_USER"
let private pwd = env "COUCHDB_PASSWORD"
let private dbServerUrl = "http://db:5984"

type UserRecord = JsonProvider<"""{
  "_id": "org.couchdb.user:dev",
  "_rev": "1-39b7182af5f4dc7a72d1782d808663b1",
  "name": "dev",
  "type": "user",
  "roles": [],
  "password_scheme": "pbkdf2",
  "iterations": 10,
  "derived_key": "492c5c2855d72ae88a5ff5f70f75ddb0ef63b32f",
  "salt": "a6212b3f03511523954fbc93e7c0907d"
}""">

type ConfigurationRecord = JsonProvider<"""{
    "_id" : "name",
    "source" : "name of source such as Azure DevOps, Rally or Jira",
    "dataset" : "name of the dataset. Eg a project name in azure devops",
    "transformations" : ["transformation 1", "transformation 2"]
}""">

type DataSet = JsonProvider<"""{
    "some column" : ["rowValue1", "rowValue2"],
    "some column2" : ["rowValue1", "rowValue2"]
}""">

type TransformationRecord = JsonProvider<"""{"lines" : ["","jghkhj"]}""">


type CacheRecord = JsonProvider<"""{
    "_id" : "name",
    "TimeStamp" : "24-09-2019",
    "Data" : {
        "columnNames" : ["a","b"],
        "values" : [["zcv"],[1.2],["2019-01-01"]]
    }
}""">

type AzureDevOpsAnalyticsRecord = JsonProvider<"""{
  "@odata.context": "https://analytics.dev.azure.com/kmddk/flowerpot/_odata/v2.0/$metadata#WorkItemRevisions(WorkItemId,WorkItemType,State,StateCategory,Iteration)",
  "value": [
    {
      "WorkItemId": 77632,
      "WorkItemType": "User Story",
      "State": "New",
      "StateCategory": "Proposed",
      "Iteration": {
        "ProjectSK": "7c0a8e77-f740-44a3-96a7-7d7ff665caaf",
        "IterationSK": "857658df-406e-43e5-bb1b-382d229f2dea",
        "IterationId": "857658df-406e-43e5-bb1b-382d229f2dea",
        "IterationName": "Iteration 1",
        "Number": 2565,
        "IterationPath": "flowerpot\\Iteration 1",
        "StartDate": "2019-02-13T00:00:00+01:00",
        "EndDate": "2019-02-27T23:59:59.999+01:00",
        "IterationLevel1": "flowerpot",
        "IterationLevel2": "Iteration 1",
        "Depth": 1,
        "IsEnded": true
      }
    }], "@odata.nextLink":"https://analytics.dev.azure.com/"}""">

type List = JsonProvider<"""{
    "total_rows": 2,
    "offset": null,
    "rows": [
        {
            "id": "id1",
            "key": "id1",
            "value": {
                "rev": "1-a4f8ac9d02e65da8251583a0c893e26b"
            },
            "doc": {
                "_id": "id1",
                "_rev": "1-a4f8ac9d02e65da8251583a0c893e26b",
                "lines": [
                    "line1",
                    "line2",
                    "line3"
                ]
            }
        }]
    }""">
type TableView = JsonProvider<""" {"columnNames" : ["a","b"], "values" : [[0,1,2,3,4],[0.4,1.2,2.4,3.5,4.1],["x","y","z"],["2019-01.01","2019-01.01"]]} """>
type private DatabaseName =
    Configurations
    | Transformations
    | Cache
    | RawData
    | Users
type HttpMethod = 
    Get
    | Post
    | Put
type Database<'a> (databaseName, parser : string -> 'a)  =
    
    let urlWithId (id : string) = 
        let dbUrl = dbServerUrl + "/" + databaseName
        id
        |> sprintf "%s/%s" dbUrl
        
    let request httpMethod silentErrors body path  =
        let m =
              match httpMethod with 
              Get -> "GET"
              | Post -> "POST"
              | Put -> "PUT"
        let url = 
            System.String.Join("/",[
                dbServerUrl
                databaseName
                path
            ])
        printfn "%sting %s from %s" m path databaseName
        let resp =
            let headers =
                [
                    HttpRequestHeaders.BasicAuth user pwd
                    HttpRequestHeaders.ContentType HttpContentTypes.Json
                ]
            match body with
            None -> 
                Http.Request(url,
                    httpMethod = m, 
                    silentHttpErrors = silentErrors,
                    headers = headers
                )
            | Some body ->
                Http.Request(url,
                    httpMethod = m, 
                    silentHttpErrors = silentErrors, 
                    body = TextRequest body,
                    headers = headers
                )
        printfn "Response status code : %d. Url: %s" resp.StatusCode resp.ResponseUrl
        resp

    let getBody (resp : HttpResponse) = 
        match resp.Body with
        Binary _ -> failwithf "Can't use a binary response"
        | Text res -> res
    let requestString httpMethod silentErrors body path = 
        request httpMethod silentErrors body path |> getBody
    let tryRequest m = request m true
    let get = requestString Get false None
    let put body = requestString Put false (Some body) 
    let post body = requestString Post false (Some body) 
    let tryGet = tryRequest Get None 
    let tryPut body = tryRequest Put (Some body)  
    let tryPost body = tryRequest Post (Some body) 
    
    member __.Get id =
        get id |> parser

    member __.TryGet id = 
        let resp = tryGet id
        let body = 
            match resp.Body with
            Text s ->
                try
                    s |> parser |> Some
                with e ->
                   eprintfn "Couldn't parse %s" s
                   None
            | _ -> 
                eprintfn "Response body was binary"
                None
        if resp.StatusCode >= 200  && resp.StatusCode <= 299 then
            body
        else
            eprintfn "Failed to get %s. StatusCode: %d. Body: %A" id resp.StatusCode body
            None

    member __.Put id body = 
        put body id |> ignore

    member __.FilterByKeys keys = 
        let body = 
           System.String.Join(",", 
               keys
               |> Seq.map(fun s -> sprintf "%A" s)
           )
           |> sprintf """{"keys" : [%s]}"""
        try
            (post body "_all_docs?include_docs=true"
             |> List.Parse).Rows
            |> Array.map(fun entry -> 
                try
                    entry.Doc.ToString() |> parser
                with e ->
                    failwithf "Failed loading transformations. Row: %A. Msg: %s" (entry.ToString()) e.Message
                )
        with _ ->
            eprintfn "Failed getting documents by key. POST Body: %s" body
            reraise()

    member __.TableView (keys : string list) = 
        let startKey = 
            System.String.Join(",", keys |> List.map (sprintf "%A")) |> sprintf "[%s]"
        let endkey =
            let reversed = 
                keys
                |> List.rev
            
            System.String.Join(",", 
                (reversed.Head + "a")::reversed.Tail 
                |> List.rev
                |> List.map (sprintf "%A") 
            ) |> sprintf "[%s]"
        let path = sprintf """_design/default/_view/table/?startkey=%s&endkey=%s""" startKey endkey
        printfn "Path used for table view %A" path
        (get path |> List.Parse).Rows
        |> Array.map(fun entry -> entry.Value.ToString() |> TableView.Parse)



let configurations = Database ("configurations", ConfigurationRecord.Parse)
let transformations = Database ("transformations", TransformationRecord.Parse)
let cache = Database ("cache", CacheRecord.Parse)
let rawdata = Database("rawdata", CacheRecord.Parse)
let users = Database ("_users", UserRecord.Parse)
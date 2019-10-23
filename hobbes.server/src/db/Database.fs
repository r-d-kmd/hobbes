module Database

open FSharp.Data

let env name = 
    System.Environment.GetEnvironmentVariable name

#if DEBUG
let private dbServerUrl = "http://localhost:5984"
let private user = "admin"
let private pwd = "password"
#else
let private dbServerUrl = "http://db:5984"
let private user = env "COUCHDB_USER"
let private pwd = env "COUCHDB_PASSWORD"
#endif

type CouchDoc = JsonProvider<"""{
    "_id" : "dd",
    "_rev": "jlkjkl"}""">

type Viewdoc = JsonProvider<"""{
    "_id" : "dd",
    "key": "jens",
    "_rev": "jlkjkl"}""">


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

type Rev = JsonProvider<"""{"_rev": "osdhfoi94392h329020"}""">
type Hash = JsonProvider<"""{"hash": "9AFDC4392329020"}""">

type DataSet = JsonProvider<"""{
    "some column" : ["rowValue1", "rowValue2"],
    "some column2" : ["rowValue1", "rowValue2"]
}""">

type AzureDevOpsAnalyticsRecord = JsonProvider<"""{
  "@odata.context": "https://analytics.dev.azure.com/kmddk/flowerpot/_odata/v2.0/$metadata#WorkItemRevisions(WorkItemId,WorkItemType,State,StateCategory,Iteration)",
  "value": [
    {
    "WorkItemId":3833,
    "Revision":3,
    "RevisedDate":"2016-12-22T10:56:27.87+01:00",
    "RevisedDateSK":20161222,
    "DateSK":20161222,
    "IsCurrent":false,
    "IsLastRevisionOfDay":false,
    "IsLastRevisionOfPeriod":"None",
    "AnalyticsUpdatedDate":"2018-12-11T23:28:29.2066667Z",
    "ProjectSK":"2139bb34-57e3-4d7d-a6e1-1c0542a45e29",
    "WorkItemRevisionSK":62809820,
    "AreaSK":"4d25b0a2-1e87-4f78-adc9-0129c4b99f94",
    "IterationSK":"63f90684-6c1f-4ecf-9e44-6e055cd5f5b4",
    "ChangedByUserSK":"7de04d29-b95b-4596-8cb4-8fa60f123d82",
    "CreatedByUserSK":"349548d0-2ecd-4a1f-ae89-eaf68681d6cd",
    "ChangedDateSK":20161222,
    "CreatedDateSK":20161219,
    "StateChangeDateSK":20161220,
    "InProgressDateSK":20161220,
    "Watermark":16800,
    "Title":"Manage templates",
    "WorkItemType":"Feature",
    "ChangedDate":"2016-12-22T09:22:40.967+01:00",
    "CreatedDate":"2016-12-19T11:19:08.42+01:00",
    "State":"User stories created",
    "Reason":"Moved to state User stories created",
    "Priority":2,
    "StackRank":1999997974.0,
    "ValueArea":"Business",
    "ParentWorkItemId":2536,
    "StateCategory":"Resolved",
    "InProgressDate":"2016-12-20T11:11:57.637+01:00",
    "StateChangeDate":"2016-12-20T11:11:57.637+01:00",
    "Count":1,"CommentCount":0,
    "Agile_Gandalf_Additionalclarification":false,
    "Iteration":{
        "ProjectSK":"2139bb34-57e3-4d7d-a6e1-1c0542a45e29",
        "IterationSK":"63f90684-6c1f-4ecf-9e44-6e055cd5f5b4",
        "IterationId":"63f90684-6c1f-4ecf-9e44-6e055cd5f5b4",
        "IterationName":"Gandalf",
        "Number":159,
        "IterationPath":"Gandalf",
        "IterationLevel1":"Gandalf",
        "Depth":0,"IsEnded":false
    },
    "Area": {
        "ProjectSK":"2139bb34-57e3-4d7d-a6e1-1c0542a45e29",
        "AreaSK":"4d25b0a2-1e87-4f78-adc9-0129c4b99f94",
        "AreaId":"4d25b0a2-1e87-4f78-adc9-0129c4b99f94",
        "AreaName":"PO team",
        "Number":171,
        "AreaPath":"Gandalf\\PO team",
        "AreaLevel1":"Gandalf",
        "AreaLevel2":"PO team",
        "Depth":1
    }
}], "@odata.nextLink":"https://analytics.dev.azure.com/"}""">

type List = JsonProvider<"""[{
    "total_rows": 2,
    "offset": 2,
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
    },{
 "id": "07e9a2611a712c808bd422425c9dcda2",
 "key": [
  "Azure DevOps",
  "flowerpot"
 ],
 "value": 90060205,
 "doc": {}}]""", SampleIsList = true>

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
    | Delete

type ViewList<'a> = 
    {
        TotalRows : int
        Offset : int
        Rows : 'a []
    }
let private getBody (resp : HttpResponse) = 
    match resp.Body with
    Binary _ -> failwithf "Can't use a binary response"
    | Text res -> res

type View(getter : string list -> ((string * string) list) option -> int * string, name) = 

    let _list (startKey : string option) (endKey : string option) limit (descending : bool option) skip = 
        let args = 
            [ 
                match  startKey, endKey  with
                  None,None -> ()
                  | Some key,None | None,Some key -> yield "key", key
                  | Some startKey,Some endKey -> 
                      yield "startkey", startKey
                      yield "endkey", endKey
                match limit with
                  None -> ()
                  | Some l -> yield "limit", string l
                if descending.IsSome && descending.Value then yield "descending","true"
                match skip with
                  None -> ()
                  | Some l -> yield "skip", string l
            ] |> Some
        let path = 
            [
             "_design"
             "default"
             "_view"
             name
            ]
        getter path args

    let getListFromResponse (statusCode,body) =
        if statusCode < 300 && statusCode >= 200 then
            body |> List.Parse
        else
            failwithf "Error: %s" body
    
    let listResult  (startKey : string option) (endKey : string option) limit (descending : bool option) skip =
        _list startKey endKey limit descending skip
        |> getListFromResponse
    
    let rowCount startKey endKey = 
        (listResult startKey endKey (Some 0) None None).TotalRows
        |> Option.orElse(Some 0)
        |> Option.get


    let list (parser : string -> 'a) (startKey : string option) (endKey : string option) (descending : bool option) = 
        let mutable limit = 128
        let rec fetch i acc = 
            printfn "Fetching with a page size of %d" limit
            let statusCode,body = _list startKey endKey (Some limit) descending (i |> Some)
            if statusCode = 500 && limit > 1 then
                //this is usually caused by an os process time out, due to too many reccords being returned
                //gradually shrink the page size and retry
                limit <- limit / 2
                fetch i acc
            else
                let result = (statusCode,body) |> getListFromResponse
                let rowCount =  result.Rows |> Array.length |> max limit
                let values = 
                    match result.Value with
                    Some v -> (v |> string)::acc
                    | _ -> result.Rows |> Array.fold(fun acc entry -> entry.Value.ToString()::acc) acc
                match result.TotalRows, result.Offset with
                Some t, Some o when t <= o + rowCount -> values
                | _ -> fetch (i + rowCount) values

        fetch 0 [] |> List.map parser
    member __.List<'a>(parser : string -> 'a, ?startKey : string, ?endKey : string, ?descending) =
        list parser startKey endKey descending
    member __.List<'a>(parser : string -> 'a, limit, ?startKey : string, ?endKey : string, ?descending) =
        (listResult startKey endKey (Some limit) descending None).Rows
        |> Array.map(fun entry -> entry.Value.ToString() |> parser)

and Database<'a> (databaseName, parser : string -> 'a) =
    let mutable _views : Map<string,View> = Map.empty
    let request httpMethod isTrial body path rev queryString =
        let enc (s : string) = System.Web.HttpUtility.UrlEncode s

        let url = 
            System.String.Join("/", [
                                        dbServerUrl
                                        databaseName
                                    ]@(path
                                       |> List.map enc))+
            match queryString with
            None -> ""
            | Some qs -> 
               "?" + System.String.Join("&",
                                     qs
                                     |> Seq.map(fun (k,v) -> sprintf "%s=%s" k  v)
               )
        let m,direction =
              match httpMethod with 
              Get -> "GET", "from"
              | Post -> "POST", "to"
              | Put -> "PUT", "to"
              | Delete -> "DELETE", "from"
            
        printfn "%sting %A %s %s" m path direction databaseName
        
        let headers =
            [
                yield HttpRequestHeaders.BasicAuth user pwd
                yield HttpRequestHeaders.ContentType HttpContentTypes.Json
                if rev |> Option.isSome then yield HttpRequestHeaders.IfMatch rev.Value
            ]
        let statusCode,body = 
            try
                let resp = 
                    match body with
                    None -> 
                        Http.Request(url,
                            httpMethod = m, 
                            silentHttpErrors = true,
                            headers = headers
                        )
                    | Some body ->
                        Http.Request(url,
                            httpMethod = m, 
                            silentHttpErrors = true, 
                            body = TextRequest body,
                            headers = headers
                        )
                resp.StatusCode, resp |> getBody
            with e ->
                500, e.Message
        let failed = statusCode < 200 || statusCode >= 300
        if failed then
            printfn "Response status code : %d.  Body: %s. Url: %s" 
                statusCode 
                (body.Substring(0,min 1000 body.Length)) 
                url
         
        if isTrial || not(failed) then
            statusCode,body
        else
            failwithf "Server error %d. Reason: %s. Url: %s" statusCode body url

    let requestString httpMethod silentErrors body path rev queryString = 
        request httpMethod silentErrors body path rev queryString |> snd
    let tryRequest m rev body path queryString = request m true body path rev queryString
    let get path = requestString Get false None path None
    let put body path rev = requestString Put false (Some body) path rev None
    let post body path = requestString Post false (Some body) path None 
    let tryGet = tryRequest Get None None 
    let tryPut body rev path = tryRequest Put rev (Some body) path None 
    let tryPost body path = tryRequest Post None (Some body) path None 
    let delete id rev = request Delete false None [id] rev None
    let handleResponse (statusCode,body : string) = 
        if  statusCode >= 200  && statusCode <= 299  then
            body
        elif statusCode = 400 then
            let length = 500
            let start =
                if (body).Contains "Invalid JSON starting at character " then
                    (((body.Split("Invalid JSON starting at character ") |> Array.last).Split ' '
                     |> Array.head).Trim()
                    |> int)
                    - 20
                    |> max 0
                else
                    0
            failwithf "Bad format. Doc: %s" (body.Substring(start, min body.Length length))
        else
            failwith body
    member this.AddView name =
        _views <- _views.Add(name, View(tryGet,name))
        this
    
    member __.ListIds() =
        (get ["_all_docs"] None
         |> List.Parse).Rows
         |> Array.map(fun r -> r.Id)
         |> Seq.ofArray

    member __.List() =
        (get ["_all_docs"] (Some ["include_docs","true"])
         |> List.Parse).Rows
         |> Array.map(fun r -> r.Doc.JsonValue.ToString JsonSaveOptions.DisableFormatting |> parser)
         |> Seq.ofArray

    member __.Get id =
        get [id] None |> parser

    member __.TryGet id = 
        let statusCode,body = tryGet [id] None
        if statusCode >= 200  && statusCode <= 299 then
            body |> parser |> Some
        else
            eprintfn "Failed to get %s. StatusCode: %d. Body: %A" id statusCode (body.Substring(0,min 500 body.Length ))
            None             

    member __.GetRev id =
        (get id None |> Rev.Parse).Rev      

    member __.TryGetRev id = 
        let statusCode,body = tryGet id None
        if statusCode >=200 && statusCode < 300 then
            let revision = 
                (body |> Rev.Parse).Rev
            (if System.String.IsNullOrWhiteSpace(revision) then 
                failwithf "Invalid revision. %s" body)
            revision |> Some
        else
            None

        
    member __.GetHash id =
         (get [sprintf "%s_hash" id] None
          |> Hash.Parse).Hash   

    member __.TryGetHash id = 
        let statusCode,body = tryGet [sprintf "%s_hash" id] None
        if statusCode >= 200 && statusCode < 300 then
            (body 
               |> Hash.Parse).Hash 
               |> Some  
        else None                        
    member __.Put(path, body, ?rev) = 
        put body path rev
    member __.Put(id, body, ?rev) = 
        put body [id] rev
    member __.TryPut(id, body, ?rev) = 
        tryPut body rev [id]
    member __.Post(body) = 
        tryPost body []
        |> handleResponse
    member __.Post(path, body) = 
        tryPost body [path]
        |> handleResponse
        
    member __.FilterByKeys keys = 
        let body = 
           System.String.Join(",", 
               keys
               |> Seq.map(fun s -> sprintf "%A" s)
           )
           |> sprintf """{"keys" : [%s]}"""
        try
            (post body ["_all_docs"] (Some ["include_docs","true"])
             |> List.Parse).Rows
            |> Array.map(fun entry -> 
                try
                    entry.Doc.ToString() |> parser
                with e ->
                    failwithf "Failed loading. Row: %A. Msg: %s" (entry.ToString()) e.Message
            ) |> Seq.ofArray
        with _ ->
            eprintfn "Failed getting documents by key. POST Body: %s" (body.Substring(0,min body.Length 500))
            reraise()
    member __.Views with get() = _views
    member this.InsertOrUpdate doc = 
        let id = (CouchDoc.Parse doc).Id
        if System.String.IsNullOrWhiteSpace id then
            failwith "Document must have a valid id"
        match [id] |> this.TryGetRev with
        None ->
            printfn "Found no rev, so assuming it's a new doc. id: %s" id
            this.Post(doc)
        | Some rev -> 
            printfn "Found rev, going to update. id: %s. rev: %s" id rev 
            this.Put(id, doc,  rev)
    member __.Delete id =
        let doc = 
            get [id] None
            |> CouchDoc.Parse
        delete id (Some doc.Rev)

let couch = Database ("", ignore)
let users = Database ("_users", UserRecord.Parse)
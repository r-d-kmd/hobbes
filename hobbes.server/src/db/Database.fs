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

type DataValues =
    Floats of (int * float) []
    | Texts of (int * string) []
    | DateTimes of (int * System.DateTime) []
    with member x.Length 
           with get() = 
               match x with
               Floats a -> a.Length
               | Texts a -> a.Length
               | DateTimes a -> a.Length
         member x.Append other =
            match x,other with
            Floats a1, Floats a2 -> a2 |> Array.append a1 |> Floats
            | Texts a1, Texts a2 -> a2 |> Array.append a1 |> Texts
            | DateTimes a1,DateTimes a2 -> a2 |> Array.append a1 |> DateTimes
            | _ -> failwithf "Incompatible types: %A %A" x other
         member x.ToSeq() =
            match x with
            Floats a -> 
                a |> Array.map(fun (i,v) -> i, box v)
            | Texts a ->
                a |> Array.map(fun (i,v) -> i, box v)
            | DateTimes a ->
                a |> Array.map(fun (i,v) -> i, box v)

type DataRecord = {
    Columns : string []
    Values : DataValues []
}

type IdRecord = JsonProvider<"""{"_id": "someID"}""">

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

type WorkItemRevisionRecord = JsonProvider<"""
        {
            "id": "id1",
            "key": 300,
            "value": 300,
            "doc":{
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
        }
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

type List = JsonProvider<"""{
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
    }""">
type CouchDoc = JsonProvider<"""{"_id" : "dd","_rev":"jlkjkl"}""">
type TableView = JsonProvider<""" {"columnNames" : ["a","b"], "values" : [[0,1,2,3,4],[0.4,1.2,2.4,3.5,4.1],["x","y","z"],["2019-01.01","2019-01.01"]]} """>
type private DatabaseName =
    Configurations
    | Transformations
    | Cache
    | RawData
    | Users

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TableView =
    let toTable (tableView : TableView.Root []) =
        tableView
        |> Array.fold(fun (count, (map : Map<_,_>)) record ->
            let values = 
                record.Values
                |> Array.map(fun raw -> 
                   match raw.Numbers with
                   [||] -> 
                       match raw.Strings with
                       [||] -> 
                           raw.DateTimes
                           |> Array.mapi (fun i dt -> i + count,dt)
                           |> DateTimes
                       | strings ->
                           strings
                           |> Array.mapi (fun i dt -> i + count,dt)
                           |> Texts
                   | numbers ->
                       numbers
                       |> Array.mapi(fun i n -> i + count, float n)
                       |> Floats
                )
            let map = 
               record.ColumnNames
               |> Array.indexed
               |> Array.fold(fun map (i,columnName) ->
                   let columnValues = values.[i]
                   match map |> Map.tryFind columnName with
                   None -> map.Add(columnName, columnValues)
                   | Some vs -> map.Add(columnName, vs.Append columnValues)
               ) map
            //Values can have empty cells in the end but needs to be aligned on the first element
            let maxLength = 
                (values
                 |> Array.maxBy(fun a -> a.Length)).Length
            count + maxLength, map
        ) (0,Map.empty)
        |> snd
        |> Map.toSeq

type HttpMethod = 
    Get
    | Post
    | Put

type ViewList<'a> = 
    {
        TotalRows : int
        Offset : int
        Rows : 'a []
    }
type IDatabase =
    abstract GetAllDocs<'a> : (string -> 'a) -> seq<'a>
    abstract Get<'a> : string -> (string -> 'a) -> 'a
    abstract TryGet : string -> (string -> 'a) -> 'a option
    abstract GetRev : string -> string
    abstract TryGetRev : string -> string option
    abstract Put: string * string * string option -> string
    abstract TryPut: string * string * string option -> HttpResponse
    abstract Post : string * string -> string
    abstract FilterByKeys<'a> : seq<string> -> (string -> 'a) -> seq<'a> 
    abstract Views : unit -> Map<string,View> with get    
    abstract Delete : string -> unit

and View(getter, name) = 
    let _list (startKey : string option) (endKey : string option) limit (descending : bool option) skip = 
            let args = 
                System.String.Join("&",
                    [ 
                        match  startKey, endKey  with
                          None,None -> ()
                          | Some key,None | None,Some key -> yield "key", key
                          | Some startKey,Some endKey -> 
                              yield "startKey", startKey
                              yield "endKey", endKey
                        match limit with
                          None -> ()
                          | Some l -> yield "limit", string l
                        if descending.IsSome && descending.Value then yield "descending","true"
                        match skip with
                          None -> ()
                          | Some l -> yield "skip", string l
                    ] |> List.map(fun (a,b) -> a + "=" + b))
            sprintf """_design/default/_view/%s/?%s""" name args
            |> getter 
            |> List.Parse
    let list (parser : string -> 'a) (startKey : string option) (endKey : string option) (descending : bool option) = 
        let rowCount = (_list startKey endKey None descending None).TotalRows
        let limit = 100
        
        //max %limit records at a time
        [|for i in 0..limit..(rowCount + limit - 1) ->
            _list startKey endKey (Some limit) None (i |> Some) |]
        |> Array.collect(fun l -> l.Rows)
        |> Array.map(fun entry -> entry.Value.ToString() |> parser)    
        
    member __.List<'a>(parser : string -> 'a, ?startKey : string, ?endKey : string, ?descending) =
        list parser startKey endKey descending
    member __.List<'a>(parser : string -> 'a, limit, ?startKey : string, ?endKey : string, ?descending) =
        (_list startKey endKey (Some limit) descending None).Rows
        |> Array.map(fun entry -> entry.Value.ToString() |> parser) 
    

and Database<'a> (databaseName, parser : string -> 'a) =
    let mutable _views : Map<string,View> = Map.empty
    let urlWithId (id : string) = 
        let dbUrl = dbServerUrl + "/" + databaseName
        id
        |> sprintf "%s/%s" dbUrl
     
    let getBody (resp : HttpResponse) = 
        match resp.Body with
        Binary _ -> failwithf "Can't use a binary response"
        | Text res -> res

    let request httpMethod silentErrors body path rev  =
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
        
        let headers =
            [
                yield HttpRequestHeaders.BasicAuth user pwd
                yield HttpRequestHeaders.ContentType HttpContentTypes.Json
                if rev |> Option.isSome then yield HttpRequestHeaders.IfMatch rev.Value
            ]
        let maxRetries = 10
        let rec requester count = 
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
            if resp.StatusCode = 500 && count < maxRetries then
                //most likely cause is that a requested view is being updated. That's a temporary problem
                //wait a random and (likely) increasing amount of time and then try again
                System.Threading.Thread.Sleep (100 * (System.Random().Next(count, count * 2 + 5))) 
                printfn "Retry #%d" count
                requester (count + 1)
            else
                resp
        let resp = requester 1
        printfn "Response status code : %d. Url: %s" resp.StatusCode resp.ResponseUrl
        if silentErrors || (resp.StatusCode >= 200 && resp.StatusCode < 300) then
            resp
        else
            failwithf "Server error %d. Reason: %s" resp.StatusCode (resp |> getBody)

    let requestString httpMethod silentErrors body path rev = 
        request httpMethod silentErrors body path rev |> getBody
    let tryRequest m rev body path = request m true body path rev
    let get path = requestString Get false None path None
    let put body path rev = requestString Put false (Some body) path rev 
    let post body path = requestString Post false (Some body) path None 
    let tryGet = tryRequest Get None None 
    let tryPut body rev = tryRequest Put rev (Some body)  
    let tryPost body = tryRequest Post None (Some body) 

    member this.AddView name =
        _views <- _views.Add(name, View(get,name))
        this
    member this.GetAllDocs() = (this :> IDatabase).GetAllDocs<'a> parser
    member this.Get id                  = (this :> IDatabase).Get id parser
    member this.TryGet id               = (this :> IDatabase).TryGet id parser           
    member this.GetRev id               = (this :> IDatabase).GetRev id               
    member this.TryGetRev id            = (this :> IDatabase).TryGetRev id            
    member this.Put(path,body, ?rev)    = (this :> IDatabase).Put(path,body, rev)    
    member this.TryPut(path,body, ?rev) = (this :> IDatabase).TryPut(path,body, rev) 
    member this.Post(path,body)         = (this :> IDatabase).Post(path,body)         
    member this.FilterByKeys keys       = (this :> IDatabase).FilterByKeys keys parser      
    member this.Views with get()        = (this :> IDatabase).Views       
    member this.Delete id               = (this :> IDatabase).Delete id               
    interface IDatabase with
        member __.GetAllDocs parser =
            (get "_all_docs"
             |> List.Parse).Rows
             |> Array.map (string >> parser)
             |> Seq.ofArray

        member __.Get id parser =
            get id |> parser

        member __.TryGet id parser = 
            let resp = tryGet id
            let body = 
                match resp.Body with
                Text s ->
                    try
                        s |> parser |> Some
                    with _ ->
                       eprintfn "Couldn't parse %s" (s.Substring(0, min 500 s.Length))
                       None
                | _ -> 
                    eprintfn "Response body was binary"
                    None
            if resp.StatusCode >= 200  && resp.StatusCode <= 299 then
                body
            else
                let body = body.Value.ToString()
                eprintfn "Failed to get %s. StatusCode: %d. Body: %A" id resp.StatusCode (body.Substring(0,min 500 body.Length ))
                None

        member __.GetRev id =
            (get id |> Rev.Parse).Rev        

        member __.TryGetRev id = 
            let resp = tryGet id
            let body = 
                match resp.Body with
                Text s ->
                    try
                        (s |> Rev.Parse).Rev |> Some
                    with _ ->
                       eprintfn "Couldn't parse %s" (s.Substring(0, min 500 s.Length))
                       None
                | _ -> 
                    eprintfn "Response body was binary"
                    None
            if resp.StatusCode >= 200  && resp.StatusCode <= 299 then
                body
            else
                let body = body.Value.ToString()
                eprintfn "Failed to get %s. StatusCode: %d. Body: %A" id resp.StatusCode (body.Substring(0,min 500 body.Length ))
                None        

        member __.Put(id, body, ?rev) = 
            put body id rev
        member __.TryPut(id, body, ?rev) = 
            tryPut body rev id
        member __.Post(path, body) = 
            let resp = tryPost body path
            if  resp.StatusCode >= 200  && resp.StatusCode <= 299  then
                resp |> getBody
            elif resp.StatusCode = 400 then
                let respB = (resp |> getBody)
                let length = 500
                let start =
                    if (respB).Contains "Invalid JSON starting at character " then
                        (((respB.Split("Invalid JSON starting at character ") |> Array.last).Split ' '
                         |> Array.head).Trim()
                        |> int)
                        - 20
                        |> max 0
                    else
                        0
                failwithf "Bad format. Doc: %s" (body.Substring(start, min body.Length length))
            else
                failwith (resp |> getBody)
        member __.FilterByKeys keys parser = 
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
                ) |> Seq.ofArray
            with _ ->
                eprintfn "Failed getting documents by key. POST Body: %s" (body.Substring(0,min body.Length 500))
                reraise()
        member __.Views with get() = _views
        
        member __.Delete id =
            let doc = 
                get id
                |> CouchDoc.Parse
           
            let url = 
                System.String.Join("/",[
                    dbServerUrl
                    databaseName
                    id
                ])
            printfn "Deleting %s from %s" id databaseName
            
            let headers =
                [
                    HttpRequestHeaders.BasicAuth user pwd
                    HttpRequestHeaders.ContentType HttpContentTypes.Json
                    HttpRequestHeaders.IfMatch doc.Rev
                ]
            
            Http.Request(url,
                httpMethod = "DELETE", 
                headers = headers
            ) |> ignore

let couch          = Database ("", ignore)
let configurations = Database ("configurations", ConfigurationRecord.Parse)
let transformations = Database ("transformations", TransformationRecord.Parse)
let cache = 
    Database("cache", CacheRecord.Parse)
      .AddView("srcproj")

let rawdata = 
    Database("rawdata", CacheRecord.Parse)
      .AddView("table")
      .AddView "WorkItemRevisions"

let users = Database ("_users", UserRecord.Parse)
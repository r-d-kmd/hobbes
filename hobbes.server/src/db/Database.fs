namespace Hobbes.Server.Db
    open FSharp.Data
    module Database =
        type Logger = string -> unit
        type LogFormatter<'a> = Printf.StringFormat<'a,unit>
        type ILog =
            abstract Log : string -> unit
            abstract Error : string -> string -> unit
            abstract Debug : string -> unit
            abstract Logf<'a> : LogFormatter<'a> -> 'a
            abstract Errorf<'a> : string -> LogFormatter<'a> -> 'a
            abstract Debugf<'a> : LogFormatter<'a> -> 'a
        
        
        let hash (input : string) =
            use md5Hash = System.Security.Cryptography.MD5.Create()
            let data = md5Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input))
            let sBuilder = System.Text.StringBuilder()
            (data
            |> Seq.fold(fun (sBuilder : System.Text.StringBuilder) d ->
                    sBuilder.Append(d.ToString("x2"))
            ) sBuilder).ToString()  
            
        let env name defaultValue = 
            match System.Environment.GetEnvironmentVariable name with
            null -> defaultValue
            | v -> v

        let private user = env "COUCHDB_USER" "admin"
        let private pwd = env "COUCHDB_PASSWORD" "password"

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

        type View(getter : string list -> ((string * string) list) option -> int * string, name, log : ILog) = 

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
                log.Debugf "Fetching view %A?%A " path args
                getter path args

            let getListFromResponse (statusCode,body) =
                if statusCode < 300 && statusCode >= 200 then
                    log.Debug "Parsing list result"
                    body |> List.Parse
                else
                    log.Errorf null "Error when fetching list: %s" body
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
                    let statusCode,body = _list startKey endKey (Some limit) descending (i |> Some)
                    if statusCode = 500 && limit > 1 then
                        //this is usually caused by an os process time out, due to too many records being returned
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

        and Database<'a> (databaseName, parser : string -> 'a, log : ILog, dbServerUrl : string) =
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
                    
                log.Debugf "%sting %A %s %s" m url direction databaseName
                
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
                    log.Debugf "Response status code : %d.  Body: %s. Url: %s" 
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
            
            let databaseOperation operationName argument =
                let path = 
                   match argument with
                   None -> [operationName]
                   | Some a -> [operationName;a]
                requestString Post false None path None

            let handleResponse (statusCode,body : string) = 
                if  statusCode >= 200  && statusCode <= 299  then
                    body
                elif statusCode = 400 then
                    failwithf "Bad format. Doc: %s" (body.Substring(0, min body.Length 500))
                else
                    failwith body
            member this.AddView name =
                _views <- _views.Add(name, View(tryGet,name,log))
                this
            
            member __.ListIds() =
                (get ["_all_docs"] None
                 |> List.Parse).Rows
                 |> Array.map(fun r -> r.Id)
                 |> Seq.ofArray

            member __.List() =
                (get ["_all_docs"] (Some ["include_docs","true"])
                 |> List.Parse).Rows
                 |> Array.map(fun r -> r.Doc.ToString() |> parser)
                 |> Seq.ofArray

            member __.Get id =
                get [id] None |> parser

            member __.Get path =
                get path None |> parser

            member __.TryGet id = 
                let statusCode,body = tryGet [id] None
                if statusCode >= 200  && statusCode <= 299 then
                    body |> parser |> Some
                else
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
                with e ->
                    log.Errorf e.StackTrace "Failed getting documents by key.Message: %s POST Body: %s" e.Message (body.Substring(0,min body.Length 500))
                    reraise()
            member __.Views with get() = _views
            member this.InsertOrUpdate doc = 
                let id = (CouchDoc.Parse doc).Id
                if System.String.IsNullOrWhiteSpace id then
                    failwith "Document must have a valid id"
                match [id] |> this.TryGetRev with
                None ->
                    log.Debugf "Found no rev, so assuming it's a new doc. id: %s" id
                    this.Post(doc)
                | Some rev -> 
                    log.Debugf "Found rev, going to update. id: %s. rev: %s" id rev 
                    this.Put(id, doc,  rev)

            member __.Compact() = 
                databaseOperation "_compact"  None
            
            member __.CompactDesign() = 
                databaseOperation "_compact" (Some "default")
            
            member __.ViewClenaup() = 
                databaseOperation "_view_cleanup" None

            member this.CompactAndClean() = 
                this.Compact() |> ignore
                this.CompactDesign() |> ignore
                this.ViewClenaup() |> ignore
            
            member __.Delete id =
                let doc = 
                    get [id] None
                    |> CouchDoc.Parse
                delete id (Some doc.Rev)
                
            new(databaseName, parser, log) = Database(databaseName, parser, log, env "DB_SERVER_URL" "http://localhost:5984")

        open FSharp.Core.Printf
        let consoleLogger =
                { new ILog with
                    member __.Log msg   = printfn "%s" msg
                    member __.Error stackTrace msg = eprintfn "%s StackTrace: \n %s" msg stackTrace
                    member __.Debug msg = printfn "%s" msg
                    member __.Logf<'a> (format : LogFormatter<'a>) = 
                        kprintf ignore format 
                    member __.Errorf<'a> _ (format : LogFormatter<'a>) = 
                        kprintf ignore format
                    member __.Debugf<'a> (format : LogFormatter<'a>) = 
                        kprintf ignore format
                }    
        let users = Database ("_users", UserRecord.Parse, consoleLogger)
        let couch = Database ("", id, consoleLogger)
       
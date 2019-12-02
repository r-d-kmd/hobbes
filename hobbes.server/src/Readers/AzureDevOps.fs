namespace Hobbes.Server.Readers
module AzureDevOps =

    open FSharp.Data
    open Hobbes.Server.Db

    let inline private asObj v =
        match v with
        Some v -> 
            v |> box
        | None -> 
            null

    let private azureFields = 
        [
             "RevisedDate", fun (row : Rawdata.AzureDevOpsAnalyticsRecord.Value) -> asObj row.RevisedDate
             "WorkItemId",  fun (row : Rawdata.AzureDevOpsAnalyticsRecord.Value) -> box row.WorkItemId 
             "IsLastRevisionOfDay" , fun (row : Rawdata.AzureDevOpsAnalyticsRecord.Value) -> asObj row.IsLastRevisionOfDay
             "Title",  fun (row : Rawdata.AzureDevOpsAnalyticsRecord.Value) -> box row.Title 
             "ChangedDate", fun (row : Rawdata.AzureDevOpsAnalyticsRecord.Value) -> asObj row.ChangedDate 
             "WorkItemType",  fun (row : Rawdata.AzureDevOpsAnalyticsRecord.Value) -> box row.WorkItemType 
             "CreatedDate", fun (row : Rawdata.AzureDevOpsAnalyticsRecord.Value) -> asObj row.ChangedDate 
             "State", fun (row : Rawdata.AzureDevOpsAnalyticsRecord.Value) -> asObj row.State 
             "StateCategory",fun (row : Rawdata.AzureDevOpsAnalyticsRecord.Value) -> asObj row.StateCategory 
             "Priority", fun (row : Rawdata.AzureDevOpsAnalyticsRecord.Value) -> asObj row.Priority 
             "LeadTimeDays", fun (row : Rawdata.AzureDevOpsAnalyticsRecord.Value) -> asObj row.LeadTimeDays 
             "CycleTimeDays", fun (row : Rawdata.AzureDevOpsAnalyticsRecord.Value) -> asObj row.CycleTimeDays          
             "WorkItemRevisionSK", fun (row : Rawdata.AzureDevOpsAnalyticsRecord.Value) -> box row.WorkItemRevisionSk
        ]

    let tryNextLink (data : string) = 
        let data = Rawdata.AzureDevOpsAnalyticsRecord.Parse data
        try
            if System.String.IsNullOrWhiteSpace(data.OdataNextLink) |> not then
                Some data.OdataNextLink
            else 
                None
        with _ -> None

    let isEmpty (data : string) = 
        let data = Rawdata.AzureDevOpsAnalyticsRecord.Parse data
        data.Value |> Array.isEmpty

    type Record = 
        {
            Index : int
            RevisedDate : System.Nullable<System.DateTimeOffset>
            WorkItemId : int
            IsLastRevisionOfDay :  System.Nullable<bool>
            Title : string
            ChangedDate :  System.Nullable<System.DateTimeOffset>
            WorkItemType : string
            CreatedDate :  System.Nullable<System.DateTimeOffset>
            State : string
            StateCategory : string
            Priority : System.Nullable<int>
            IterationIterationLevel1 : string
            IterationIterationLevel2 : string
            IterationIterationLevel3 : string
            IterationIterationLevel4 : string
            IterationNumber : int
            LeadTimeDays :  System.Nullable<decimal>
            CycleTimeDays :  System.Nullable<decimal>
        }

    let private serialiseValue (value : obj) = 
                match value with
                | null -> "null"
                | :? string as s -> sprintf """ "%s" """ s
                | :? bool as b -> 
                    if b then "true" else "false"
                | :? int as i -> i |> string
                | :? float as f -> f |> string
                | :? decimal as d -> d |> string
                | :? System.DateTime as d -> sprintf """ "%s" """ (d.ToString())
                | :? System.DateTimeOffset as d -> sprintf """ "%s" """ (d.ToString())
                | _ -> sprintf "%A" value

    let private getInitialUrl (account,projectName) =
        let initialUrl = 
            let selectedFields = 
               (",", azureFields |> List.map fst) |> System.String.Join
            sprintf "https://analytics.dev.azure.com/%s/%s/_odata/v2.0/WorkItemRevisions?$expand=Iteration&$select=%s,Iteration&$filter=IsLastRevisionOfDay%%20eq%%20true%%20and%%20WorkItemRevisionSK%%20gt%%20%d" account projectName selectedFields
        try
            match  DataConfiguration.AzureDevOps (account,projectName) |> Rawdata.tryLatestId with
            Some workItemRevisionId -> 
                initialUrl workItemRevisionId
            | None -> 
                printfn "Didn't get a work item revision id"
                initialUrl 0L
        with e -> 
            eprintfn "Failed to get latest. Message: %s" e.Message
            initialUrl 0L
            
    let private request user pwd httpMethod body url  =
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

    let private hash (input : string) =
            use md5Hash = System.Security.Cryptography.MD5.Create()
            let data = md5Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input))
            let sBuilder = System.Text.StringBuilder()
            (data
            |> Seq.fold(fun (sBuilder : System.Text.StringBuilder) d ->
                    sBuilder.Append(d.ToString("x2"))
            ) sBuilder).ToString()

    let readCached project =
        let raw = 
            project
            |> DataConfiguration.AzureDevOps 
            |> Rawdata.bySource
        let rows = 
            raw
            |> Seq.mapi(fun index row ->
                let iterationProperties =
                    match row.Iteration with
                    Some iteration ->
                        [
                           "Iteration.IterationLevel1", asObj iteration.IterationLevel1 
                           "Iteration.IterationLevel2", asObj iteration.IterationLevel2 
                           "Iteration.IterationLevel3", asObj iteration.IterationLevel3 
                           "Iteration.IterationLevel4", asObj iteration.IterationLevel4
                           "Iteration.Number", iteration.Number |> box
                        ]
                    | None -> 
                        []
                let properties = 
                    azureFields
                    |> List.map(fun (name, getter) ->
                        name, getter row
                    )
                index,(iterationProperties@properties)
            )
        rows

    //TODO should be async and in parallel-ish
    let sync azureToken project cacheRevision = 
        let source = DataConfiguration.AzureDevOps project
        let rec _read hashes (url : string) : unit  = 
            let resp = 
                url
                |> request azureToken azureToken "GET" None
            if resp.StatusCode = 200 then
                let body = 
                    match resp.Body with
                    Text body ->
                        body
                        |> Some
                    | _ -> 
                        None
                let rawId =  (url |> hash)
                let hashes = rawId::hashes
                body
                |> Option.bind(fun body -> 
                    if body |> isEmpty |> not then
                        let body = 
                            body.Replace("\\\"","'")
                        let rawdataRecord = Cache.createDataRecord rawId source body [
                                                                                        "url", url
                                                                                        "recordCount", hashes 
                                                                                                       |> List.length 
                                                                                                       |> string
                                                                                        "hashes", System.String.Join(",", hashes) 
                                                                                                  |> sprintf "[%s]"
                                                                                     ] 
                        Rawdata.InsertOrUpdate rawdataRecord |> ignore     
                    body
                    |> tryNextLink
                ) |> Option.map(fun nextlink ->   
                       printfn "Continuing with %s" nextlink
                       _read hashes nextlink
                ) |> ignore
            else 
                eprintfn "StatusCode: %d. Message: %s" resp.StatusCode (match resp.Body with Text t -> t | _ -> "")
                
        project
        |> getInitialUrl
        |> _read []
        
        200,sprintf """ {"synced" : "%A", "status" : "ok"} """ project
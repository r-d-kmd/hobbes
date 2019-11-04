namespace Hobbes.Server.Readers
module AzureDevOps =

    open FSharp.Data
    open Deedle
    open Hobbes.Server.Db

    let private azureFields = 
        [
         "ChangedDate"
         "WorkITemId"
         "WorkItemRevisionSK"
         "WorkItemType"
         "State"
         "StateCategory"
         "LeadTimeDays"
         "CycleTimeDays"
         "Iteration"
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
            LeadTimeDays :  System.Nullable<decimal>
            CycleTimeDays :  System.Nullable<decimal>
        }

    let private serialiseValue (value : obj) = 
                match value with
                | null -> "null"
                | :? string as s -> sprintf """ "%s" """ s
                | :? bool as b -> b |> string
                | :? int as i -> i |> string
                | :? float as f -> f |> string
                | :? decimal as d -> d |> string
                | :? System.DateTime as d -> sprintf """ "%s" """ (d.ToString())
                | :? System.DateTimeOffset as d -> sprintf """ "%s" """ (d.ToString())
                | _ -> sprintf "%A" value

    let private getInitialUrl projectName =
        let initialUrl = 
            let selectedFields = 
               (",", azureFields) |> System.String.Join
            sprintf "https://analytics.dev.azure.com/kmddk/%s/_odata/v2.0/WorkItemRevisions?$expand=Iteration&$select=%s&$filter=IsLastRevisionOfDay%%20eq%%20true%%20and%%20WorkItemRevisionSK%%20gt%%20%d" projectName selectedFields
        try
            match  DataConfiguration.AzureDevOps projectName |> Rawdata.tryLatestId with
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

    let readCached (projectName : string) =
        let raw = 
            projectName 
            |> DataConfiguration.AzureDevOps 
            |> Rawdata.bySource
        raw
        |> Seq.mapi(fun index row ->
            let inline asObj m = 
                match m with
                Some v -> v |> box
                | None -> null
            Hobbes.Parsing.AST.KeyType.Create index,
                match row.Iteration with
                  Some iteration ->
                    [
                       "Iteration.IterationLevel1" => asObj iteration.IterationLevel1 
                       "Iteration.IterationLevel2" => asObj iteration.IterationLevel2 
                       "Iteration.IterationLevel3" => asObj iteration.IterationLevel3 
                       "Iteration.IterationLevel4" => asObj iteration.IterationLevel4 
                    ]
                  | None -> []
                |> List.append [
                                     "RevisedDate" => asObj row.RevisedDate
                                     "WorkItemId" =>  box row.WorkItemId 
                                     "IsLastRevisionOfDay"  => asObj row.IsLastRevisionOfDay
                                     "Title" =>  box row.Title 
                                     "ChangedDate" => asObj row.ChangedDate 
                                     "WorkItemType" =>  box row.WorkItemType 
                                     "CreatedDate" =>asObj row.ChangedDate 
                                     "State" => asObj row.State 
                                     "StateCategory" =>asObj row.StateCategory 
                                     "Priority" => asObj row.Priority 
                                     "LeadTimeDays" => asObj row.LeadTimeDays 
                                     "CycleTimeDays" => asObj row.CycleTimeDays 
                                 ]
        )

    //TODO should be async and in parallel-ish
    let sync azureToken projectName cacheRevision = 
        let source = DataConfiguration.AzureDevOps projectName
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
                
        projectName
        |> getInitialUrl
        |> _read []
        
        200,"ok"
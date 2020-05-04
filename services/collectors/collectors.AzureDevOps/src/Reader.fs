namespace Collector.AzureDevOps

open FSharp.Data
open Collector.AzureDevOps.Db
open Collector.AzureDevOps.Db.Rawdata
open Hobbes.Server.Db
open Hobbes.Shared.CommonFunctions

module Reader =
    
    type private AzureDevOpsAnalyticsRecord = JsonProvider<"""{
      "@odata.context": "https://analytics.dev.azure.com/kmddk/flowerpot/_odata/v2.0/$metadata#WorkItemRevisions(WorkItemId,WorkItemType,State,StateCategory,Iteration)",
      "timeStamp" : "ojsdfidsj",
      "value": [
        {
        "WorkItemId":3833,
        "RevisedDate":"2016-12-22T10:56:27.87+01:00",
        "IsCurrent":false,
        "IsLastRevisionOfDay":false,
        "WorkItemRevisionSK":62809820,
        "Title":"Manage templates",
        "WorkItemType":"Feature",
        "ChangedDate":"2016-12-22T09:22:40.967+01:00",
        "CreatedDate":"2016-12-19T11:19:08.42+01:00",
        "ClosedDate": "2018-09-13T16:14:24.323+02:00",
        "State":"User stories created",
        "Priority":2,
        "StateCategory":"Resolved",
        "StoryPoints": 2.0,
        "LeadTimeDays" : 200.1,
        "CycleTimeDays" : 9080.122,
        "Area": {
                "AreaPath": "Momentum"
            },
        "Iteration":{
            "IterationName":"Gandalf",
            "Number":159,
            "StartDate": "2017-05-15T00:00:00+02:00",
            "EndDate": "2017-05-28T23:59:59.999+02:00",
            "IterationPath":"Gandalf",
            "IterationLevel1":"Gandalf",
            "IterationLevel2":"Gandalf",
            "IterationLevel3":"Gandalf",
            "IterationLevel4":"Gandalf"
        }},{
        "WorkItemId":3833,
        "WorkItemRevisionSK":62809820,
        "Title":"Manage templates",
        "WorkItemType":"Feature",
        "Iteration":{
            "IterationName":"Gandalf",
            "Number":159,
            "IterationPath":"Gandalf"
        }},{
            "WorkItemId":3833,
            "WorkItemRevisionSK":62809820,
            "Title":"Manage templates",
            "WorkItemType":"Feature"
        }
    ], "@odata.nextLink":"https://analytics.dev.azure.com/"}""">

    //Helper method for optional properties of the data record
    let inline private asObj v =
        match v with
        Some v -> 
            v |> box
        | None -> 
            null

    //looks whether it's the last record or there's a odatanextlink porperty 
    //which signals that the data has been paged and that we're not at the last page yet
    let private tryNextLink (data : string) = 
        let data = AzureDevOpsAnalyticsRecord.Parse data
        try
            if System.String.IsNullOrWhiteSpace(data.OdataNextLink) |> not then
                Some data.OdataNextLink
            else 
                None
        with _ -> None
    //If there's no work item records returned, this will return true
    let isEmpty (data : string) = 
        let data = AzureDevOpsAnalyticsRecord.Parse data
        data.Value |> Array.isEmpty
    
    //TODO: Why are these fields commented out??
    let private azureFields : (string * (AzureDevOpsAnalyticsRecord.Value -> obj) ) list= 
        [
             "WorkItemId",  fun row -> box row.WorkItemId
             "ChangedDate", fun row -> asObj row.ChangedDate 
             "WorkItemType",  fun row -> box row.WorkItemType 
             "CreatedDate", fun row -> asObj row.CreatedDate
             "ClosedDate", fun row -> asObj row.ClosedDate
             "State", fun row -> asObj row.State 
             "StateCategory",fun row -> asObj row.StateCategory 
             "LeadTimeDays", fun row -> asObj row.LeadTimeDays 
             "CycleTimeDays", fun row -> asObj row.CycleTimeDays
             "StoryPoints", fun row -> asObj row.StoryPoints
             //"RevisedDate", fun row -> asObj row.RevisedDate       
             //"Priority", fun row -> asObj row.Priority 
             //"IsLastRevisionOfDay" , fun row -> asObj row.IsLastRevisionOfDay
             //"Title",  fun row -> box row.Title    
             //"WorkItemRevisionSK", fun row -> box row.WorkItemRevisionSk
        ]    
    //The first url to start with, if there's already some stored data
    let private getInitialUrl (config : AzureDevOpsConfig.Root)=
        let account = 
            let acc = config.Account.Replace("_", "-")
            if System.String.IsNullOrWhiteSpace(acc) then "kmddk"
            else acc

        let filters = 
            System.String.Join(" and ",
                [
                    "IsLastRevisionOfDay", "eq", "true"
                    "WorkItemType", "ne", "'Task'"
                    "IsCurrent", "eq", "true"
                ] |> List.map(fun (a,b,c) -> sprintf "%s %s %s" a b c)
            ).Replace(" ", "%20")
            
        let initialUrl = 
            let selectedFields = 
               (",", azureFields |> List.map fst) |> System.String.Join
            let path = 
                (sprintf "/_odata/v2.0/WorkItemRevisions?$expand=Iteration,Area&$select=%s,Iteration&$filter=%s and WorkItemRevisionSK gt " selectedFields filters).Replace(" ", "%20")

            sprintf "https://analytics.dev.azure.com/%s/%s%s%d" account config.Project path
        try
            match (config |> searchKey) |> Rawdata.tryLatestId with
            Some workItemRevisionId -> 
                initialUrl workItemRevisionId
            | None -> 
                Hobbes.Web.Log.log "Didn't get a work item revision id"
                initialUrl 0L
        with e -> 
            Hobbes.Web.Log.errorf e.StackTrace "Failed to get latest. Message: %s" e.Message
            initialUrl 0L

    //sends a http request   
    let private request user pwd httpMethod body url  =
        let headers =
            [
                HttpRequestHeaders.BasicAuth user pwd
                HttpRequestHeaders.ContentType HttpContentTypes.Json
            ]       
        match body with
        None ->
            Http.AsyncRequest(url,
                httpMethod = httpMethod, 
                silentHttpErrors = true,
                headers = headers
            ) |> Async.RunSynchronously
        | Some body ->
            Http.Request(url,
                httpMethod = httpMethod, 
                silentHttpErrors = true, 
                body = TextRequest body,
                headers = headers
            )

    let formatRawdataCache searchKey (timeStamp : string ) rawdataCache =
        assert((System.String.IsNullOrWhiteSpace searchKey) |> not)
        let jsonString (s : string) = 
            "\"" +
             s.Replace("\\","\\\\")
              .Replace("\"","\\\"") 
            + "\""
        let columnNames = 
            (",", [
                    "TimeStamp"
                    "Area.AreaPath"
                    "Iteration.IterationPath"
                    "Iteration.IterationLevel1"
                    "Iteration.IterationLevel2"
                    "Iteration.IterationLevel3"
                    "Iteration.IterationLevel4"
                    "Iteration.StartDate"
                    "Iteration.EndDate"
                    "Iteration.Number"
                ] @ (azureFields |> List.map fst)
                |> List.map jsonString)
            |> System.String.Join
            |> sprintf "[%s]"
        let rows = 
            (",",rawdataCache
                |> Seq.map(fun (row : AzureDevOpsAnalyticsRecord.Value) ->
                    let iterationProperties =
                        match (row.Iteration) with
                        Some iteration ->
                            [
                               box iteration.IterationPath
                               asObj iteration.IterationLevel1 
                               asObj iteration.IterationLevel2 
                               asObj iteration.IterationLevel3 
                               asObj iteration.IterationLevel4
                               asObj iteration.StartDate
                               asObj iteration.EndDate
                               iteration.Number |> box
                            ]
                        | None -> []
                    let areaProperty =
                        match row.Area with
                        Some area -> box area.AreaPath
                        | None -> null
                    let properties = 
                        azureFields
                        |> List.map(fun (name, getter) ->
                            getter row
                        )
                    let timeStamp =
                        box timeStamp
                    (",",
                     (timeStamp::areaProperty::iterationProperties@properties)
                     |> List.map(fun v -> 
                        match v with 
                        null -> "null"
                        | :? string as s -> 
                            jsonString s
                        | :? System.DateTime as d -> 
                            d |> string |> jsonString
                        | :? System.DateTimeOffset as d -> 
                            d.ToLocalTime() |> string |> jsonString
                        | v -> string v
                    )) |> System.String.Join
                    |> sprintf "[%s]"
                )) |> System.String.Join
                |> sprintf "[%s]"        
        sprintf """{
           "searchKey" : "%s",
           "columnNames" : %s,
           "rows" : %s,
           "rowCount" : %d
           }
        """ searchKey columnNames rows (rawdataCache |> Seq.length)

    //Reads data from the raw data store. This should be exposed as part of the API in some form 
    let read (config : AzureDevOpsConfig.Root) =
        let searchKey = (config |> searchKey)

        assert(System.String.IsNullOrWhiteSpace searchKey |> not)

        let timeStamp = 
            (match config |> Rawdata.searchKey |> Rawdata.getState with
            Some s -> 
                ((s |> Cache.CacheRecord.Parse).TimeStamp
                 |> System.DateTime.Parse)
            | None -> System.DateTime.Now).ToString("dd/MM/yyyy H:mm").Replace(":", ";")

        let raw = 
            config
            |> bySource
            |> Option.bind((formatRawdataCache searchKey timeStamp) >> Some)

        Hobbes.Web.Log.logf "\n\n azure devops:%s \n\n" (config.JsonValue.ToString())        
        raw

    //TODO should be async and in parallel-ish
    //part of the API (see server to how it's exposed)
    //we might want to store azureToken as an env variable
    let sync azureToken (config : AzureDevOpsConfig.Root) = 
        
        let rec _read hashes url = 
            Hobbes.Web.Log.logf "syncing with %s@%s" azureToken url
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
                match body with
                Some body when body |> isEmpty |> not ->

                    let body' = 
                        body.Replace("\\\"","'")
                    let conf = 
                        config.JsonValue.ToString()
                        |> parseConfiguration
                    let rawdataRecord = createDataRecord rawId (config |> searchKey) body' [
                                                                                    "url", url
                                                                                    "recordCount", hashes 
                                                                                                   |> List.length 
                                                                                                   |> string
                                                                                    "hashes", System.String.Join(",", hashes) 
                                                                                              |> sprintf "[%s]"
                                                                                 ] 
                    insertOrUpdate rawdataRecord

                    body
                    |> tryNextLink
                    |> Option.iter(fun nextlink ->   
                           Hobbes.Web.Log.logf "Continuing with %s" nextlink
                           _read hashes nextlink
                    )
                    
                | _ -> 
                    ()
            else 
                let message = 
                    match resp.Body with 
                    Text t -> t 
                    | _ -> ""
                failwith <| sprintf "StatusCode: %d. Message: %s" resp.StatusCode message
        
        let url = 
            config
            |> getInitialUrl                                   
        try
            url
            |> _read []
            200,"ok"
        with e ->
            let msg = sprintf "failed to sync Message: %s Url: %s" e.Message url
            Hobbes.Web.Log.errorf e.StackTrace "%s" msg
            500, msg
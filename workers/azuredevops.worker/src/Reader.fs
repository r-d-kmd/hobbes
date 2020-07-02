namespace Readers.AzureDevOps

open FSharp.Data
open Readers.AzureDevOps.Data
open Hobbes.Helpers.Environment
open Hobbes.Web.RawdataTypes
open Hobbes.Web
open Hobbes.Web.Cache
module Reader =

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
    let private azureFields : (string * (AzureDevOpsAnalyticsRecord.Value -> Value) ) list= 
        [
             "WorkItemId",  fun row -> row.WorkItemId |> Value.Create
             "ChangedDate", fun row -> row.ChangedDate |> Value.Bind
             "WorkItemType",  fun row -> row.WorkItemType |> Value.Create
             "CreatedDate", fun row -> row.CreatedDate |> Value.Bind
             "ClosedDate", fun row -> row.ClosedDate |> Value.Bind
             "State", fun row -> row.State |> Value.Bind
             "StateCategory",fun row -> row.StateCategory |> Value.Bind
             "LeadTimeDays", fun row -> row.LeadTimeDays |> Value.Bind
             "CycleTimeDays", fun row -> row.CycleTimeDays |> Value.Bind
             "StoryPoints", fun row -> row.StoryPoints |> Value.Bind
             //"RevisedDate", fun row -> row.RevisedDate |> Value.Bind
             //"Priority", fun row -> row.Priority |> Value.Bind
             //"IsLastRevisionOfDay" , fun row -> row.IsLastRevisionOfDay |> Value.Bind
             //"Title",  fun row -> row.Title |> Value.Create
             //"WorkItemRevisionSK", fun row -> row.WorkItemRevisionSk |> Value.Create
        ]    
    //The first url to start with, if there's already some stored data
    let private getInitialUrl (source : AzureDevOpsSource.Root)=
        if System.String.IsNullOrWhiteSpace source.Server then 
            let account = 
                let acc = source.Account.Replace("_", "-")
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

                sprintf "https://analytics.dev.azure.com/%s/%s%s%d" account source.Project path
            let key = source.JsonValue.ToString() |> keyFromSourceDoc 
            try
                match key |> Data.tryLatestId with
                Some workItemRevisionId -> 
                    initialUrl workItemRevisionId
                | None -> 
                    Log.debugf "Didn't get a work item revision id for %s" key
                    initialUrl 0L
            with e -> 
                Log.excf e "Failed to get latest for (%s)" key 
                initialUrl 0L
        else source.Server + "/_odata/v2.0/WorkItemRevisions?$expand=Iteration,Area&$select=WorkItemId,ChangedDate,WorkItemType,CreatedDate,ClosedDate,State,StateCategory,LeadTimeDays,CycleTimeDays,StoryPoints,Iteration&$filter=IsLastRevisionOfDay%20eq%20true%20and%20WorkItemType%20ne%20'Task'%20and%20WorkItemRevisionSK%20gt%200"

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

    let formatRawdataCache key (timeStamp : string ) rawdataCache =
        assert((System.String.IsNullOrWhiteSpace key) |> not)
        
        let columnNames = 
            [
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
            |> Array.ofList
        let rows : Value [] [] = 
            rawdataCache
            |> Seq.map(fun (row : AzureDevOpsAnalyticsRecord.Value) ->
                let iterationProperties =
                    match (row.Iteration) with
                    Some iteration ->
                        [
                           iteration.IterationPath |> Value.Create
                           iteration.IterationLevel1 |> Value.Bind
                           iteration.IterationLevel2 |> Value.Bind
                           iteration.IterationLevel3 |> Value.Bind
                           iteration.IterationLevel4 |> Value.Bind
                           iteration.StartDate |> Value.Bind
                           iteration.EndDate |> Value.Bind
                           iteration.Number |> Value.Create
                        ]
                    | None -> []
                let areaProperty =
                    match row.Area with
                    Some area -> area.AreaPath |> Value.Create
                    | None -> Value.Null
                let properties = 
                    azureFields
                    |> List.map(fun (name, getter) ->
                        getter row
                    )
                
                (Value.Create(timeStamp)::areaProperty::iterationProperties@properties)
                |> Array.ofList
                
            ) |> Array.ofSeq
        let data = 
            {
               ColumnNames = columnNames
               Values = rows
               RowCount = rows.Length
            } : Cache.DataResult
            
        key, data

    //Reads data from the raw data store. This should be exposed as part of the API in some form 
    let read (source : AzureDevOpsSource.Root) =
        let key = source.JsonValue.ToString() |> keyFromSourceDoc

        assert(System.String.IsNullOrWhiteSpace key |> not)

        let timeStamp = System.DateTime.Now.ToString("dd/MM/yyyy H:mm")

        let raw = 
            source
            |> bySource
            |> Option.bind((formatRawdataCache key timeStamp) >> Some)

        Log.logf "\n\n azure devops:%s \n\n" (source.JsonValue.ToString())        
        raw

    let sync azureToken (source : AzureDevOpsSource.Root) = 
        
        let rec _read hashes url = 
            Log.logf "syncing with %s@%s" azureToken url
            let resp = 
                url
                |> request azureToken azureToken "GET" None                 
            if resp.StatusCode = 200 then
                let body = 
                    resp |> Http.readBody
                let rawId =  (url |> hash)
                let hashes = rawId::hashes
                match body with
                _ when body |> isEmpty |> not ->

                    let body' = 
                        body.Replace("\\\"","'")
                    
                    let rawdataRecord =
                        sprintf """{
                            "_id" : "%s",
                            "timeStamp" : "%s",
                            "data" : %s,
                            "url" : "%s",
                            "source" : %s,
                            "recordCount" : %d,
                            "hashes" : [%s]
                            }""" rawId 
                                 (System.DateTime.Now.ToString()) 
                                 body' 
                                 url 
                                 (source.JsonValue.ToString()) 
                                 hashes.Length
                                 (System.String.Join(",",hashes |> Seq.map(sprintf """ "%s" """))) 
                    insertOrUpdate rawdataRecord

                    body
                    |> tryNextLink
                    |> Option.iter(fun nextlink ->   
                           Log.logf "Continuing with %s" nextlink
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
            source
            |> getInitialUrl                                   
        try
            url
            |> _read []
            200,"ok"
        with e ->
            let msg = sprintf "failed to sync Message: %s Url: %s" e.Message url
            Log.exc e msg
            500, msg
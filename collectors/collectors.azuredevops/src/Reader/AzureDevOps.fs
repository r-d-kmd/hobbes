namespace Collector.AzureDevOps.Reader
module AzureDevOps =

    open FSharp.Data
    open Collector.AzureDevOps.Db
    open Hobbes.Server.Db
    open Hobbes.Shared.RawdataTypes

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
    
    //The first url to start with, if there's already some stored data
    let private getInitialUrl ((account : string),projectName) =
        let account = account.Replace("_", "-")
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

            sprintf "https://analytics.dev.azure.com/%s/%s%s%d" account projectName path
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

    //Used to create the ID of the individual raw data records.
    //There might be a better ID now that the database is project specific
    //Eg the last workitemid contained, which would help in knowing where to start the next sync
    let private hash (input : string) =
            use md5Hash = System.Security.Cryptography.MD5.Create()
            let data = md5Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input))
            let sBuilder = System.Text.StringBuilder()
            (data
            |> Seq.fold(fun (sBuilder : System.Text.StringBuilder) d ->
                    sBuilder.Append(d.ToString("x2"))
            ) sBuilder).ToString()

    let formatRawdataCache (timeStamp : string ) rawdataCache =
        let jsonString (s : string) = 
            "\"" +
             s.Replace(",", "\u066B") //"It's a comma but won't interfer with CSV format/parsing"
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
            ("",rawdataCache
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
                        null -> ""
                        | :? string as s -> 
                            jsonString s
                        | v -> string v
                    )) |> System.String.Join
                    |> sprintf "[%s]"
                )) |> System.String.Join
                |> sprintf "[%s]"
        sprintf """{
           "names" : %s,
           "rows" : %s
           }
        """ columnNames rows

    //Reads data from the raw data store. This should be exposed as part of the API in some form 
    let read account project =
        let timeStamp = 
            (match Rawdata.getState (sprintf "azure devops:%s" project) with
            Some s -> 
                ((s |> Cache.CacheRecord.Parse).TimeStamp
                 |> System.DateTime.Parse)
            | None -> System.DateTime.Now).ToString("dd/MM/yyyy H:mm").Replace(":", ";")

        let raw = 
            (account, project)
            |> DataConfiguration.AzureDevOps 
            |> Rawdata.bySource
            |> formatRawdataCache timeStamp

        Hobbes.Web.Log.logf "\n\n azure devops:%s \n\n" project     

        raw

    //TODO should be async and in parallel-ish
    //part of the API (see server to how it's exposed)
    //we might want to store azureToken as an env variable
    let sync azureToken project = 
        let source = DataConfiguration.AzureDevOps project
        let rec _read hashes url = 
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
                    let rawdataRecord = Cache.createDataRecord rawId source body' [
                                                                                    "url", url
                                                                                    "recordCount", hashes 
                                                                                                   |> List.length 
                                                                                                   |> string
                                                                                    "hashes", System.String.Join(",", hashes) 
                                                                                              |> sprintf "[%s]"
                                                                                 ] 
                    Rawdata.InsertOrUpdate rawdataRecord |> Async.Start

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

        try
            project
            |> getInitialUrl                                   
            |> _read []
            200,"ok"
        with e ->
            let msg = sprintf "failed to sync Message: %s" e.Message
            Hobbes.Web.Log.errorf e.StackTrace "%s" msg
            500, msg
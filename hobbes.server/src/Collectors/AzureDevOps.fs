namespace Hobbes.Server.Collectors

open FSharp.Data
open Hobbes.Shared.RawdataTypes

module AzureDevOps =

    let url = "http://azuredevopscollector-svc:8085/"

    let readBody = function
    | Binary b -> System.Text.Encoding.ASCII.GetString b
    | Text t -> t

    let request method path =
        let response = Http.Request(url+path, httpMethod = method, silentHttpErrors = true)
        response.StatusCode, (readBody response.Body)

    let requestNoTimeOut method path =
        let response = 
            Http.Request(url+path, 
                         httpMethod = method, 
                         silentHttpErrors = true, 
                         customizeHttpRequest = (fun request -> request.Timeout <- System.Int32.MaxValue ; request)
                        )
        response.StatusCode, (readBody response.Body)        
    
    let postNoTimeOut path body=
        let response = 
            Http.Request(url+path, 
                         httpMethod = HttpMethod.Post, 
                         silentHttpErrors = true, 
                         customizeHttpRequest = (fun request -> request.Timeout <- System.Int32.MaxValue ; request),
                         body = HttpRequestBody.TextRequest body
                        )
        response.StatusCode, (readBody response.Body)        

    let getNoTimeOut path =
        requestNoTimeOut HttpMethod.Get path

    let get path =
        request HttpMethod.Get path

    let delete path =
        request HttpMethod.Delete path   

    let listRawdata() =
        get "admin/list/rawdata"     

    let deleteRaw id =
        sprintf "admin/raw/%s" id 
        |> delete    

    let clearRawdata() =
        get "admin/clear/rawdata"  

    let getSyncState syncId =
        sprintf "status/sync/%s" syncId
        |> get

    let createSyncDoc account project revision =
        sprintf "admin/createSyncDoc/%s/%s/%s" account project revision
        |> get     

    let setSync completed account project revision msg =
        sprintf "admin/setSync/%s/%s/%s/%s/%s" (string completed) account project revision msg
        |> get

    let setSyncCompleted account project revision =
        setSync "true" account project revision "-"    

    let setSyncFailed account project revision msg =
        setSync "false" account project revision msg      

    let _sync account project =
        sprintf "data/sync/%s/%s" account project
        |> requestNoTimeOut HttpMethod.Get

    let sync conf =
        let status,resp = postNoTimeOut "data/sync/" conf
        //TODO this obviously needs to go
        if status < 400 && status >= 200  then 
            setSyncCompleted conf "" ""
        else 
            setSyncFailed "" "" "" ""
        |> ignore
        status,resp  

    let getRaw id =
        sprintf "admin/raw/%s" id
        |> get    

    let read conf =
        let status,response = postNoTimeOut "data/read" conf
        if status < 300 && status >= 200 then 
            let data = 
                response 
                |> Hobbes.Server.Db.DataConfiguration.Data.Parse
            let columnNames = data.Names
            
            data.Values
            |> Seq.mapi(fun index row ->
                index,  row
                        |> Seq.map(fun r -> 
                            match r.JsonValue with
                            JsonValue.String v -> box v
                            | JsonValue.Number v -> box v
                            | JsonValue.Float v -> box v
                            | JsonValue.Boolean v -> box v
                            | JsonValue.Null -> null
                            | e -> failwithf "only simple values allowed but got %A" e
                        ) |> Seq.zip columnNames
            )
        else failwithf "Got an unexpected response: %d - %s" status response
         

    [<System.Obsolete>]
    let _read account project =
        let formatRawdataCache rawdataCache (timeStamp : string ) =
            rawdataCache
            |> Seq.mapi(fun index (row : AzureDevOpsAnalyticsRecord.Value) ->
                let iterationProperties =
                    match (row.Iteration) with
                    Some iteration ->
                        [
                           "Iteration.IterationPath", box iteration.IterationPath
                           "Iteration.IterationLevel1", asObj iteration.IterationLevel1 
                           "Iteration.IterationLevel2", asObj iteration.IterationLevel2 
                           "Iteration.IterationLevel3", asObj iteration.IterationLevel3 
                           "Iteration.IterationLevel4", asObj iteration.IterationLevel4
                           "Iteration.StartDate", asObj iteration.StartDate
                           "Iteration.EndDate", asObj iteration.EndDate
                           "Iteration.Number", iteration.Number |> box
                        ]
                    | None -> []
                let areaProperty =
                    match row.Area with
                    Some area ->
                        [
                            "Area.AreaPath", box area.AreaPath
                        ]
                    | None -> []
                let properties = 
                    azureFields
                    |> List.map(fun (name, getter) ->
                        name, getter row
                    )
                let timeStamp =
                    ["TimeStamp", box timeStamp]            
                index,(iterationProperties@areaProperty@properties@timeStamp)
            )   
        let rawDataAndTS = (sprintf "data/readCached/%s/%s" account project
                           |> getNoTimeOut
                           |> snd
                           |> AzureDevOpsAnalyticsRecord.Parse)      
        formatRawdataCache rawDataAndTS.Value  rawDataAndTS.TimeStamp             


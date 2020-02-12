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
        let response = Http.Request(url+path, httpMethod = method, silentHttpErrors = true, customizeHttpRequest = (fun request -> request.Timeout <- System.Int32.MaxValue ; request))
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

    let sync account project =
        sprintf "data/sync/%s/%s" account project
        |> requestNoTimeOut HttpMethod.Get      

    let getRaw id =
        sprintf "admin/raw/%s" id
        |> get    

    let formatRawdataCache rawdataCache =
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
            index,(iterationProperties@areaProperty@properties)
        )   

    let readCached account project =
        (sprintf "data/readCached/%s/%s" account project
        |> getNoTimeOut
        |> snd
        |> AzureDevOpsAnalyticsRecord.Parse).Value
        |> formatRawdataCache                   


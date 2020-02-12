namespace Hobbes.Server.Collectors

open FSharp.Data

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

    type RawdataCache = JsonProvider<"""{
        "data" : [[["string1", "object1"], ["string1", null]]]
    }""">    

    let formatRawdataCache rawdataCache =
        (rawdataCache
        |> RawdataCache.Parse).Data
        |> Array.mapi (fun i x -> i, x 
                                     |> Array.map (fun y -> (let v = if y.Length < 2 then null else y.[1] 
                                                            y.[0], unbox v))
                                     |> List.ofArray
                      )
        |> List.ofArray     

    let readCached account project =
        sprintf "data/readCached/%s/%s" account project
        |> getNoTimeOut
        |> snd
        |> formatRawdataCache                      


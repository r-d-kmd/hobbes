namespace Hobbes.Server.Collectors

open FSharp.Data
open Hobbes.Server.Db

module Collector =

    
    let private collectorUrl (collectorName : string) path = 
        let url = sprintf "http://%scollector-svc:8085/%s" (collectorName.Replace(" ","")) path
        url

    let private readBody = function
    | Binary b -> System.Text.Encoding.ASCII.GetString b
    | Text t -> t

    let private request method collectorName path =
        let response = Http.Request(collectorUrl collectorName path, httpMethod = method, silentHttpErrors = true)
        response.StatusCode, (readBody response.Body)

    let private requestNoTimeOut method collectorName path =
        let response = 
            Http.Request(collectorUrl collectorName path, 
                         httpMethod = method, 
                         silentHttpErrors = true, 
                         customizeHttpRequest = (fun request -> request.Timeout <- System.Int32.MaxValue ; request)
                        )
        response.StatusCode, (readBody response.Body)        
    
    let private postNoTimeOut collectorName path body =
        let url = collectorUrl collectorName path
        printfn "url of post: %s" url
        let response = 
            Http.Request(url,
                         httpMethod = HttpMethod.Post, 
                         silentHttpErrors = true, 
                         customizeHttpRequest = (fun request -> request.Timeout <- System.Int32.MaxValue ; request),
                         body = HttpRequestBody.TextRequest body
                        )
        let resp = readBody response.Body
        printfn "body: %s" resp
        response.StatusCode, resp

    let private getNoTimeOut path =
        requestNoTimeOut HttpMethod.Get path

    let private post path body =
        let url = collectorUrl "azuredevops" path
        printfn "** posting to %s. path: %s body: %s" url path body
        let response = 
            Http.Request(url, 
                         httpMethod = HttpMethod.Post, 
                         silentHttpErrors = true,
                         body = HttpRequestBody.TextRequest body
                        )

        response.StatusCode, (readBody response.Body)

    [<System.Obsolete("Azure specific")>]
    let private get collectorName path =
        request HttpMethod.Get  collectorName path

    [<System.Obsolete("Azure specific")>]
    let private delete path =
        request HttpMethod.Delete  "azuredevops" path   

    [<System.Obsolete("Azure specific")>]
    let internal listRawdata() =
        get "azuredevops" "admin/list/rawdata"     

    [<System.Obsolete("Azure specific")>]
    let internal deleteRaw id =
        sprintf "admin/raw/%s" id 
        |> delete    

    [<System.Obsolete("Azure specific")>]
    let internal clearRawdata() =
        get "azuredevops" "admin/clear/rawdata"  

    
    let internal getSyncState collectorName syncId =
        try
            sprintf "status/sync/%s" syncId
            |> get collectorName 
        with _ -> 
            failwith "Not implemented" //200,"{}" TODO needs to be completed

    [<System.Obsolete("Azure specific")>]
    let private setSync completed account project revision msg =
        sprintf "admin/setSync/%s/%s/%s/%s/%s" (string completed) account project revision msg
        |> get"azuredevops" 

    [<System.Obsolete("Azure specific")>]
    let private setSyncCompleted account project revision =
        setSync "true" account project revision "-"    

    [<System.Obsolete("Azure specific")>]
    let private setSyncFailed account project revision msg =
        setSync "false" account project revision msg      

    let sync conf =
        let collectorName = (conf |> DataConfiguration.ConfigurationRecord.Parse).Source.ToLower()
        let status,response = postNoTimeOut collectorName "data/sync" conf
        //TODO this obviously needs to go
        if status < 400 && status >= 200  then 
            setSyncCompleted conf "" ""
        else 
            setSyncFailed "" "" "" ""
        |> ignore
        status,response  
        
    [<System.Obsolete("Azure specific")>]
    let getRaw id =
        sprintf "admin/raw/%s" id
        |> get "azuredevops"    

    let read conf =
        let collectorName = (conf |> DataConfiguration.ConfigurationRecord.Parse).Source.ToLower()
        let status,response = postNoTimeOut collectorName "data/read" conf
        if status < 300 && status >= 200 then 
            let data = 
                response 
                |> DataConfiguration.Data.Parse
            let columnNames = data.ColumnNames
            
            data.Rows
            |> Seq.mapi(fun index row ->
                index,row.JsonValue.AsArray()
                      |> Seq.map(fun v ->
                          match v with
                          JsonValue.String s -> box s
                          | JsonValue.Null -> null
                          | JsonValue.Number n -> box n
                          | JsonValue.Float f -> box f
                          | JsonValue.Boolean b -> box b
                          | v -> failwithf "Only simple values expected but got %A" v
                      ) |> Seq.zip columnNames
            )    
        else 
            failwithf "Got an unexpected response: %d - %s" status response
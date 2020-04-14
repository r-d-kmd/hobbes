namespace Hobbes.Server.Collectors

open FSharp.Data

module AzureDevOps =

    let private collectorUrl collectorName path= sprintf "http://%scollector-svc:8085/%s" collectorName path

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
    
    let private postNoTimeOut collectorName path body=
        let response = 
            Http.Request(collectorUrl collectorName path,
                         httpMethod = HttpMethod.Post, 
                         silentHttpErrors = true, 
                         customizeHttpRequest = (fun request -> request.Timeout <- System.Int32.MaxValue ; request),
                         body = HttpRequestBody.TextRequest body
                        )
        response.StatusCode, (readBody response.Body)        

    let private getNoTimeOut path =
        requestNoTimeOut HttpMethod.Get path

    let private get path =
        request HttpMethod.Get  "azuredevops" path

    let private delete path =
        request HttpMethod.Delete  "azuredevops" path   

    [<System.Obsolete("Azure specific")>]
    let internal listRawdata() =
        get "admin/list/rawdata"     

    [<System.Obsolete("Azure specific")>]
    let internal deleteRaw id =
        sprintf "admin/raw/%s" id 
        |> delete    

    [<System.Obsolete("Azure specific")>]
    let internal clearRawdata() =
        get "admin/clear/rawdata"  

    [<System.Obsolete("Azure specific")>]
    let internal getSyncState syncId =
        sprintf "status/sync/%s" syncId
        |> get

    [<System.Obsolete("Azure specific")>]
    let internal createSyncDoc account project revision =
        sprintf "admin/createSyncDoc/%s/%s/%s" account project revision
        |> get     

    [<System.Obsolete("Azure specific")>]
    let private setSync completed account project revision msg =
        sprintf "admin/setSync/%s/%s/%s/%s/%s" (string completed) account project revision msg
        |> get

    [<System.Obsolete("Azure specific")>]
    let private setSyncCompleted account project revision =
        setSync "true" account project revision "-"    

    [<System.Obsolete("Azure specific")>]
    let private setSyncFailed account project revision msg =
        setSync "false" account project revision msg      

    [<System.Obsolete("Azure specific")>]
    let sync conf =
        let collectorName = (conf |> Hobbes.Server.Db.DataConfiguration.ConfigurationRecord.Parse).Source.Value.ToLower()
        let status,response = postNoTimeOut collectorName "data/sync/" conf
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
        |> get    

    let read conf =
        let collectorName = (conf |> Hobbes.Server.Db.DataConfiguration.ConfigurationRecord.Parse).Source.Value.ToLower()
        let status,response = postNoTimeOut collectorName "data/read/" conf
        if status < 300 && status >= 200 then 
            let data = 
                response 
                |> Hobbes.Server.Db.DataConfiguration.Data.Parse
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
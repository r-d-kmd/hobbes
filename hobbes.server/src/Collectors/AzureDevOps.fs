namespace Hobbes.Server.Collectors

open FSharp.Data

module AzureDevOps =

    let private url = "http://azuredevopscollector-svc:8085/"

    let private readBody = function
    | Binary b -> System.Text.Encoding.ASCII.GetString b
    | Text t -> t

    let private request method path =
        let response = Http.Request(url+path, httpMethod = method, silentHttpErrors = true)
        response.StatusCode, (readBody response.Body)

    let private requestNoTimeOut method path =
        let response = 
            Http.Request(url+path, 
                         httpMethod = method, 
                         silentHttpErrors = true, 
                         customizeHttpRequest = (fun request -> request.Timeout <- System.Int32.MaxValue ; request)
                        )
        response.StatusCode, (readBody response.Body)        
    
    let private postNoTimeOut path body=
        let response = 
            Http.Request(url+path, 
                         httpMethod = HttpMethod.Post, 
                         silentHttpErrors = true, 
                         customizeHttpRequest = (fun request -> request.Timeout <- System.Int32.MaxValue ; request),
                         body = HttpRequestBody.TextRequest body
                        )
        response.StatusCode, (readBody response.Body)        

    let private getNoTimeOut path =
        requestNoTimeOut HttpMethod.Get path

    let private get path =
        request HttpMethod.Get path

    let private delete path =
        request HttpMethod.Delete path   

    let internal listRawdata() =
        get "admin/list/rawdata"     

    let internal deleteRaw id =
        sprintf "admin/raw/%s" id 
        |> delete    

    let internal clearRawdata() =
        get "admin/clear/rawdata"  

    let internal getSyncState syncId =
        sprintf "status/sync/%s" syncId
        |> get

    let internal createSyncDoc account project revision =
        sprintf "admin/createSyncDoc/%s/%s/%s" account project revision
        |> get     

    let private setSync completed account project revision msg =
        sprintf "admin/setSync/%s/%s/%s/%s/%s" (string completed) account project revision msg
        |> get

    let private setSyncCompleted account project revision =
        setSync "true" account project revision "-"    

    let private setSyncFailed account project revision msg =
        setSync "false" account project revision msg      

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
        let status,response = postNoTimeOut "data/read/" conf
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
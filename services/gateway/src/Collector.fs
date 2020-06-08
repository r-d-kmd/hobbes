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
        Hobbes.Web.Log.debugf "url of post: %s" url
        let response = 
            Http.Request(url,
                         httpMethod = HttpMethod.Post, 
                         silentHttpErrors = true, 
                         customizeHttpRequest = (fun request -> request.Timeout <- System.Int32.MaxValue ; request),
                         body = HttpRequestBody.TextRequest body
                        )
        let resp = readBody response.Body
        Hobbes.Web.Log.debugf "body: %s" resp
        response.StatusCode, resp

    let private getNoTimeOut path =
        requestNoTimeOut HttpMethod.Get path

    let private post path body =
        let url = collectorUrl "azuredevops" path
        Hobbes.Web.Log.debugf "** posting to %s. path: %s body: %s" url path body
        let response = 
            Http.Request(url, 
                         httpMethod = HttpMethod.Post, 
                         silentHttpErrors = true,
                         body = HttpRequestBody.TextRequest body
                        )

        response.StatusCode, (readBody response.Body)

    let private get collectorName path =
        request HttpMethod.Get  collectorName path 
    
    let sync conf =
        let collectorName = (conf |> DataConfiguration.parse).Source.ToLower()
        let status,response = postNoTimeOut collectorName "data/sync" conf
        status,response

    let read conf =
        let collectorName = (conf |> DataConfiguration.parse).Source.ToLower()
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
            Hobbes.Web.Log.errorf  "Got an unexpected response: %d - %s. Configuration: %s" status response conf
            Seq.empty
namespace Collector.AzureDevOps.Services

open Hobbes.Server.Routing
open Collector.AzureDevOps.Db
open Hobbes.Server.Db
open Collector.AzureDevOps.Reader
open Hobbes.Web
open Hobbes.Helpers


[<RouteArea ("/data", false)>]
module Data =

    let synchronize source token =
        try
            match source with
            DataConfiguration.AzureDevOps(account,projectName) -> 
                let statusCode,body = AzureDevOps.sync token (account,projectName)
                Log.logf "Sync finised with statusCode %d and result %s" statusCode body
                if statusCode < 200 || statusCode >= 300 then 
                    let msg = sprintf "Syncronization failed. Message: %s" body
                    eprintfn "%s" msg
                statusCode, body                 
            | source -> 
                let msg = sprintf "Error: The source %s, wasn't AzureDevOps" source.SourceName
                eprintfn "%s" msg
                404, msg
        with e ->
            eprintfn "Sync failed due to exception: %s" e.Message
            404, e.Message                                                                   
             
    [<Get ("/sync/%s/%s")>]
    let sync ((account : string), (project : string)) =
        let dataSource = DataConfiguration.DataSource.AzureDevOps (account, project)
        Rawdata.clearProject dataSource
        let token = (env (sprintf "AZURE_TOKEN_%s" <| account.ToUpper().Replace("-","_")) null)
        synchronize dataSource token   

    [<Get ("/readCached/%s/%s")>]
    let raw (account, project) =
        let res = AzureDevOps.readCached account project 
                  |> Seq.map (fun (_, nv) -> let jsonfied = nv
                                                            |> List.map (fun (n, v) -> (sprintf """["%s", "%A"]""" n v).Replace("\"\"", "\"") )
                                             System.String.Join(",", jsonfied)
                                             |> sprintf """[%s]"""
                             )
                             
        200, System.String.Join(",", res)  
             |> sprintf """{"stuff" : [%s]}"""              
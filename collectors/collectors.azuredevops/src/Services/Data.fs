namespace Collector.AzureDevOps.Services

open Hobbes.Server.Routing
open Collector.AzureDevOps.Db
open Hobbes.Server.Db
open Collector.AzureDevOps.Reader
open Hobbes.Web
open Hobbes.Helpers
open FSharp.Data


[<RouteArea ("/data", false)>]
module Data =
    type private Config = JsonProvider<"""{
            "_id" : "name",
            "source" : "azuredevops",
            "account" : "kmddk",
            "project" : "gandalf"
        }""">
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
             
    [<Post ("/sync/", true)>]
    let sync confDoc =
        let conf = Config.Parse confDoc
        let dataSource = DataConfiguration.DataSource.AzureDevOps (conf.Account, conf.Project)
        Rawdata.clearProject dataSource
        let token = (env (sprintf "AZURE_TOKEN_%s" <| conf.Account.ToUpper().Replace("-","_")) null)
        synchronize dataSource token   

    [<Post ("/read/", true)>]
    let read confDoc =
        let conf = Config.Parse confDoc
        let raw, timeStamp = AzureDevOps.read conf.Account conf.Project
        let res = (",", Seq.map (fun x -> x.ToString()) raw)
                  |> System.String.Join
        200, sprintf """{"columnNames" : [%s],
                         ""
                         "timeStamp" : "%s"}""" res timeStamp
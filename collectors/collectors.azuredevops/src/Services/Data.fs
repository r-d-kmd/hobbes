namespace Collector.AzureDevOps.Services

open Hobbes.Server.Routing
open Collector.AzureDevOps.Db
open Hobbes.Server.Db
open Collector.AzureDevOps.Reader
open Hobbes.Web
open Hobbes.Helpers
open FSharp.Data
open Hobbes.Shared.RawdataTypes

[<RouteArea ("/data", false)>]
module Data =
   
    let synchronize (config : Config.Root) token =
        try
            let statusCode, body = AzureDevOps.sync token config
            Log.logf "Sync finised with statusCode %d and result %s" statusCode body
            if statusCode < 200 || statusCode >= 300 then 
                let msg = sprintf "Syncronization failed. Message: %s" body
                eprintfn "%s" msg
            statusCode, body                 
        with e ->
            eprintfn "Sync failed due to exception: %s" e.Message
            404, e.Message                                                                   
             
    [<Post ("/sync", true)>]
    let sync confDoc =
        let conf = Hobbes.Shared.RawdataTypes.Config.Parse confDoc
        Admin.createSyncDoc conf |> ignore
        Rawdata.clearProject conf
        let account = 
            if System.String.IsNullOrWhiteSpace conf.Account then
                "kmddk"
            else
                conf.Account

        let tokenName = (sprintf "AZURE_TOKEN_%s" <| account.ToUpper().Replace("-","_"))
        let token = (env tokenName null)

        Log.logf "Using token from %s=%s " tokenName token
        synchronize conf token   

    [<Post ("/read", true)>]
    let read confDoc =
        let conf = Config.Parse confDoc
        let raw = AzureDevOps.read conf
        match raw with
        Some rawData ->
            200, rawData
        | None -> 404,"No data found"
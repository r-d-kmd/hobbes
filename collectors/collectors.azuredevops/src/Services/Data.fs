namespace Collector.AzureDevOps.Services

open Hobbes.Server.Routing
open Collector.AzureDevOps.Db
open Hobbes.Server.Db
open Hobbes.Web
open Hobbes.Helpers
open Collector.AzureDevOps.Db.Rawdata
open Collector.AzureDevOps

[<RouteArea ("/data", false)>]
module Data =
    
    let synchronize (config : AzureDevOpsConfig.Root) token =
        try
            let statusCode, body = Reader.sync token config
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
        let azureDevOpsConfig = parseConfiguration confDoc
        let isSyncing = 
            match Rawdata.getState (azureDevOpsConfig |> searchKey) with
            None -> false
            | Some stateDoc -> 
                let stateDoc = Cache.CacheRecord.Parse stateDoc
                let state = stateDoc.State
                            |> Cache.SyncStatus.Parse
                state = Cache.SyncStatus.Started
        if isSyncing |> not then
            Admin.createSyncDoc azureDevOpsConfig |> ignore
            Rawdata.clearProject azureDevOpsConfig
            let account = 
                if System.String.IsNullOrWhiteSpace azureDevOpsConfig.Account then
                    "kmddk"
                else
                    azureDevOpsConfig.Account

            let tokenName = (sprintf "AZURE_TOKEN_%s" <| account.ToUpper().Replace("-","_"))
            let token = (env tokenName null)

            Log.logf "Using token from %s=%s " tokenName token
            synchronize azureDevOpsConfig token
        else
            409, "Syncronizing"   

    [<Post ("/read", true)>]
    let read confDoc =
        let conf = parseConfiguration confDoc
        
        let raw = Reader.read conf
        match raw with
        Some rawData ->
            200, rawData
        | None -> 404,"No data found"
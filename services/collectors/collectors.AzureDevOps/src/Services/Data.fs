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
                Log.errorf null "%s" msg
            statusCode, body                 
        with e ->
            Log.errorf e.StackTrace "Sync failed due to exception: %s" e.Message
            404, e.Message                                                                   
        
    [<Post ("/read", true)>]
    let read confDoc =
        let conf = parseConfiguration confDoc
        
        let raw = Reader.read conf
            
        match raw with
        Some rawData ->
            let result = rawData |> Hobbes.Shared.RawdataTypes.DataResult.Parse
            
            assert(result.ColumnNames.Length > 0)
            assert(result.RowCount = result.Rows.Length)
            assert(result.RowCount = 0 || result.ColumnNames.Length = result.Rows.[0].Numbers.Length + result.Rows.[0].Strings.Length)

            Log.logf "Data returned: %s" rawData

            200, rawData
        | None -> 
            404,"No data found"
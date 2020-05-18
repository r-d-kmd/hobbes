namespace Collector.AzureDevOps.Services

open Hobbes.Web.Routing
open Hobbes.Web
open Hobbes.Helpers
open Collector.AzureDevOps.Db.Rawdata
open Collector.AzureDevOps
open Hobbes.Web.RawdataTypes

[<RouteArea ("/data", false)>]
module Data =
    
    let synchronize (config : Config.Root) token =
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
        Log.logf "Reading data for configuration: %s" confDoc
        let conf = parseConfiguration confDoc
        let key = conf |> keyFromConfig
        let raw = Reader.read conf
            
        match raw with
        Some rawData ->
            let result = rawData |> DataResult.Parse
            
            assert(result.ColumnNames.Length > 0)
            assert(result.RowCount = result.Rows.Length)
            assert(result.RowCount = 0 || result.ColumnNames.Length = result.Rows.[0].Numbers.Length + result.Rows.[0].Strings.Length)

            Log.logf "Data returned: %s" rawData

            200, (Cache.createCacheRecord key rawData).JsonValue.ToString()
        | None -> 
            404,"No data found"
    
    [<Post ("/sync", true)>]
    let sync confDoc =
        let conf = parseConfiguration confDoc
        let source = conf.Source |> source2AzureSource
        let token = 
            if source.Account.ToString() = "kmddk" then
                env "AZURE_TOKEN_KMDDK" null
            else
                env "AZURE_TOKEN_TIME_PAYROLL_KMDDK" null

        match synchronize conf token with
        200,_ ->
            match Http.post (Http.UniformData Http.Update) id confDoc with
            Http.Success _ -> 200,"updated"
            | Http.Error(status,msg) -> status,msg
        | status,errorMessage -> 
            Log.errorf null "Failed updating uniform. Status: %d Message %s" status errorMessage
            status,errorMessage